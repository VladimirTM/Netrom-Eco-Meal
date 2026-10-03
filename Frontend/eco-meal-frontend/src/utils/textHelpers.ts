export function getInitial(name: string | null | undefined): string {
  return !name ? "?" : name[0].toUpperCase();
}
