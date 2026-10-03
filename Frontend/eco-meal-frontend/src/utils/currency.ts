// Replaces the server-side CultureInfo("ro-RO") formatting now that money is rendered in the
// browser instead of server-rendered Razor markup.
const formatter = new Intl.NumberFormat("ro-RO", { style: "currency", currency: "RON" });

export function formatCurrency(amount: number): string {
  return formatter.format(amount);
}
