import type { APIRequestContext } from '@playwright/test';
import { mailpitUrl } from './accounts';

interface MailpitMessageSummary {
  ID: string;
  To: { Address: string }[];
  Created: string;
}

interface MailpitMessagesResponse {
  messages: MailpitMessageSummary[];
}

interface MailpitFullMessage {
  Text: string;
  HTML: string;
}

// Polls Mailpit for the newest message to `toEmail` and pulls the first link out of its body
// matching `linkPattern` — used to drive the confirm-email / reset-password flows without a
// real mailbox. Retries for a few seconds since SmtpEmailSender delivers asynchronously.
export async function getLatestEmailLink(
  request: APIRequestContext,
  toEmail: string,
  linkPattern: RegExp,
  timeoutMs = 10_000,
): Promise<string> {
  const deadline = Date.now() + timeoutMs;
  let lastError: unknown;

  while (Date.now() < deadline) {
    try {
      const listRes = await request.get(`${mailpitUrl}/api/v1/messages`, { params: { limit: 50 } });
      const list = (await listRes.json()) as MailpitMessagesResponse;

      const candidates = list.messages
        .filter((m) => m.To?.some((t) => t.Address.toLowerCase() === toEmail.toLowerCase()))
        .sort((a, b) => new Date(b.Created).getTime() - new Date(a.Created).getTime());

      for (const candidate of candidates) {
        const msgRes = await request.get(`${mailpitUrl}/api/v1/message/${candidate.ID}`);
        const msg = (await msgRes.json()) as MailpitFullMessage;
        const match = (msg.HTML || msg.Text).match(linkPattern);
        if (match) return match[0];
      }
    } catch (err) {
      lastError = err;
    }

    await new Promise((resolve) => setTimeout(resolve, 500));
  }

  throw new Error(`No email to ${toEmail} matching ${linkPattern} arrived within ${timeoutMs}ms${lastError ? `: ${lastError}` : ''}`);
}
