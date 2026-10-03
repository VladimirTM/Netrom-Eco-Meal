import { useTheme } from "../../../context/ThemeContext/theme-context";

function ThemeToggle({ triggerClass = "" }: { triggerClass?: string }) {
  const { theme, toggleTheme } = useTheme();
  const isDark = theme === "dark";

  return (
    <button
      type="button"
      className={triggerClass}
      title="Toggle dark mode"
      aria-label="Toggle dark mode"
      onClick={toggleTheme}
    >
      <i className={`bi ${isDark ? "bi-sun" : "bi-moon-stars"}`} />
    </button>
  );
}

export default ThemeToggle;
