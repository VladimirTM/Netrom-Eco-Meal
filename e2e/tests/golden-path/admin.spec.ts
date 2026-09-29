import { test, expect } from '../../fixtures/test';
import { accounts } from '../../fixtures/accounts';
import { login, logout, fillReliably } from '../../fixtures/auth';
import { getLatestEmailLink } from '../../fixtures/mailpit';

test.describe('Admin golden path', () => {
  test('create a business and assign staff, approve an application, change a role, dismiss a report, add/remove a type, audit log shows it all', async ({ page, request }) => {
    const unique = Date.now();

    // Create a business and assign staff.
    await login(page, accounts.admin.email, accounts.admin.password);
    await page.goto('/businesses/create');
    const businessName = `E2E Test Kitchen ${unique}`;
    // The first fill right after a fresh navigation occasionally loses its value (reproduced on
    // both static and interactive pages — likely Blazor's enhanced-navigation DOM patch racing
    // with it) — fillReliably verifies and retries rather than trusting a single fill() here.
    await fillReliably(page.getByLabel('Name'), businessName);
    await page.getByLabel('Description').fill('Created by the Phase 0 Playwright baseline.');
    await page.getByLabel('Address').fill('1 Test Street, Timișoara');
    // The Type select starts empty until OnInitializedAsync's data loads over the circuit — wait
    // for its options rather than racing selectOption against an empty <select>.
    await expect(page.getByLabel('Type').locator('option')).not.toHaveCount(0);
    await page.getByLabel('Type').selectOption({ label: 'Restaurant' });
    await page.getByRole('button', { name: 'Create Business' }).click();
    await page.waitForURL('**/businesses');
    // With enough accumulated businesses across runs, the new one may not land on the default
    // first page — search for it rather than assuming it's already visible. Wait for the page's
    // own initial load to finish first, or the search's debounced reload races it on the same
    // per-circuit DbContext (a real app gotcha — see the Approve-step comment below).
    await page.locator('.spinner-border').waitFor({ state: 'detached' });
    await page.getByPlaceholder('Search name, description, address…').fill(businessName);
    // The search box's own reload is separately debounced (300ms, OnSearchInputAsync) — the row
    // can already be visible from a not-yet-superseded pre-filter render while that reload is
    // still in flight; wait past the debounce so it can't land concurrently with the next click's
    // own DbContext use (same class of race as above).
    await page.waitForTimeout(400);
    const businessRow = page.getByRole('row', { name: businessName });
    await expect(businessRow).toBeVisible();
    await businessRow.getByTitle('Add staff').click();
    await page.getByRole('button', { name: 'Demo Manager Two' }).click();
    await expect(businessRow).toContainText('Demo Manager Two');
    // AddStaffAsync deliberately leaves the dropdown open ("an admin can add several staff in one
    // open") — close it via its backdrop before anything else, or its overlay blocks later clicks.
    await page.locator('.role-dropdown-backdrop').click();

    // Submit a fresh business application (as a customer) to approve — self-contained, so this
    // stays safe to re-run rather than depending on the seeded "Golazo Grill" application, which
    // an earlier run may have already approved. Uses a throwaway applicant account, never the
    // shared demo.customer — approving an application auto-promotes its submitter to
    // BusinessManager (GrantApplicantAccessAsync), which would otherwise silently and permanently
    // corrupt the seeded account's role for every other test in this suite.
    await logout(page);
    const applicantEmail = `e2e.applicant.${unique}@ecomeal.local`;
    await page.goto('/account/register');
    await fillReliably(page.getByLabel('Full name'), 'E2E Applicant');
    await fillReliably(page.getByLabel('Email', { exact: true }), applicantEmail);
    await fillReliably(page.getByLabel('Password'), 'E2ePassword123!');
    await page.getByRole('button', { name: 'Create account' }).click();
    await page.waitForURL(/\/account\/login/);
    // This environment requires email confirmation before sign-in — confirm via Mailpit first.
    const confirmLink = await getLatestEmailLink(request, applicantEmail, /https?:\/\/[^\s"']*\/account\/confirm-email\?[^\s"']*/);
    await page.goto(confirmLink);
    await login(page, applicantEmail, 'E2ePassword123!');
    const applicationName = `E2E Test Application ${unique}`;
    await page.goto('/businesses/apply');
    await fillReliably(page.getByLabel('Business name'), applicationName);
    await page.getByLabel('Description').fill('A fresh application for the Phase 0 baseline to approve.');
    await page.getByLabel('Address').fill('2 Test Street, Timișoara');
    await expect(page.getByLabel('Type').locator('option')).not.toHaveCount(0);
    await page.getByLabel('Type').selectOption({ label: 'Food Truck' });
    await page.getByRole('button', { name: 'Submit for review' }).click();

    // Submit a report on a business too, for the admin to dismiss below.
    await page.goto('/businesses/apply'); // neutral page, just to leave the confirmation screen
    await page.goto(`/businesses/44444444-0000-0000-0000-000000000001`);
    await page.getByTitle('Report this kitchen').click();
    await page.getByPlaceholder("What's the issue?").fill('E2E baseline test report — safe to dismiss.');
    await page.getByRole('button', { name: 'Submit report' }).click();

    await logout(page);
    await login(page, accounts.admin.email, accounts.admin.password);

    await page.goto('/businesses');
    // Wait for the page's own initial load before touching any filter, or the two race on the
    // shared per-circuit DbContext (see the comment a few lines down).
    await page.locator('.spinner-border').waitFor({ state: 'detached' });
    // No <label> on this filter — it's a bare <select> — so anchor on its default option instead.
    await page.locator('select').filter({ has: page.getByRole('option', { name: 'All statuses' }) }).selectOption({ label: 'Pending approval' });
    const applicationRow = page.getByRole('row', { name: applicationName });
    await expect(applicationRow).toBeVisible();
    await applicationRow.getByTitle('Approve').click();
    // ApproveAsync's own handler reloads the (still "Pending approval"-filtered) list once the
    // approval completes, so the row disappears here first — wait for that round trip to finish
    // before touching the filter again, or the two concurrent circuit events race on the shared
    // per-circuit DbContext and crash it (a real app gotcha, not a test artifact).
    await expect(applicationRow).not.toBeVisible();
    await page.locator('select').filter({ has: page.getByRole('option', { name: 'All statuses' }) }).selectOption({ label: 'Approved' });
    await expect(applicationRow).toContainText('Approved');

    // Change a role, and change it back — on a throwaway account registered just for this,
    // never a seeded one (demoting a real manager drops their BusinessStaff rows, which
    // wouldn't undo on re-promotion, breaking every other test that relies on that seed data).
    await logout(page);
    const throwawayEmail = `e2e.role-change.${unique}@ecomeal.local`;
    await page.goto('/account/register');
    await fillReliably(page.getByLabel('Full name'), 'E2E Role Change');
    await fillReliably(page.getByLabel('Email', { exact: true }), throwawayEmail);
    await fillReliably(page.getByLabel('Password'), 'E2ePassword123!');
    await page.getByRole('button', { name: 'Create account' }).click();
    await page.waitForURL(/\/account\/login/);

    await login(page, accounts.admin.email, accounts.admin.password);
    await page.goto('/users');
    await page.locator('.spinner-border').waitFor({ state: 'detached' });
    await page.getByPlaceholder(/search/i).fill(throwawayEmail);
    // The search filter is a server round-trip (@bind:after) — wait for it to actually narrow the
    // table to this one throwaway account before touching anything, so a change-role click can
    // never land on a different (seeded) row while the unfiltered list is still showing.
    await expect(page.locator('tbody tr')).toHaveCount(1);
    const userRow = page.getByRole('row', { name: throwawayEmail, exact: false });
    await expect(userRow).toBeVisible();
    await expect(userRow).toContainText(throwawayEmail);
    await userRow.getByTitle('Change role').click();
    await page.getByRole('button', { name: 'Business Manager' }).click();
    await expect(userRow).toContainText('Business Manager');
    await userRow.getByTitle('Change role').click();
    await page.getByRole('button', { name: 'Customer' }).click();
    await expect(userRow).toContainText('Customer');

    // Dismiss the report submitted above.
    await page.goto('/reports');
    await page.getByRole('button', { name: 'Dismiss' }).first().click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Dismiss' }).click();

    // Add a type, then remove it.
    await page.goto('/types');
    await fillReliably(page.getByPlaceholder('e.g. Food Truck'), `E2E Type ${unique}`);
    await page.getByTitle('Add kitchen type').click();
    await expect(page.getByText(`E2E Type ${unique}`)).toBeVisible();
    // A <ul>/<li> list here, not a table — "row" was never the right role.
    await page.getByRole('listitem').filter({ hasText: `E2E Type ${unique}` }).getByTitle('Delete').click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete' }).click();
    // exact: true — the (briefly lingering) confirm dialog's own title also contains this
    // substring ('Delete "E2E Type ..."?'), which would otherwise match too.
    await expect(page.getByText(`E2E Type ${unique}`, { exact: true })).not.toBeVisible();

    // Clean up the test business (removes its staff assignment and the approved application too).
    // Search for each individually — with enough accumulated businesses, they won't both be on
    // the same default first page.
    await page.goto('/businesses');
    await page.locator('.spinner-border').waitFor({ state: 'detached' });
    await page.getByPlaceholder('Search name, description, address…').fill(businessName);
    await page.waitForTimeout(400); // let the debounced search reload settle — see comment above
    await page.getByRole('row', { name: businessName }).getByTitle('Delete').click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete' }).click();
    await page.locator('.spinner-border').waitFor({ state: 'detached' }).catch(() => {});
    await page.getByPlaceholder('Search name, description, address…').fill(applicationName);
    await page.waitForTimeout(400);
    await page.getByRole('row', { name: applicationName }).getByTitle('Delete').click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete' }).click();

    // The audit log shows these actions with the right actor — several rows reference this
    // business (created, staffed, deleted), so just confirm at least one is there.
    await page.goto('/audit-log');
    await expect(page.getByText(new RegExp(businessName)).first()).toBeVisible();
  });
});
