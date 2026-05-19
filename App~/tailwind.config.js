/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        "bg-0": "var(--bg-0)",
        "bg-1": "var(--bg-1)",
        "bg-2": "var(--bg-2)",
        "bg-3": "var(--bg-3)",
        "bg-4": "var(--bg-4)",
        "bg-5": "var(--bg-5)",

        line:        "var(--line)",
        "line-soft": "var(--line-soft)",
        "line-hard": "var(--line-hard)",

        "txt-1": "var(--txt-1)",
        "txt-2": "var(--txt-2)",
        "txt-3": "var(--txt-3)",
        "txt-4": "var(--txt-4)",
        "txt-5": "var(--txt-5)",

        "brand-violet":      "var(--violet)",
        "brand-violet-soft": "var(--violet-soft)",
        "brand-violet-deep": "var(--violet-deep)",
        "brand-cyan":        "var(--cyan)",
        "brand-cyan-soft":   "var(--cyan-soft)",

        ok:   "var(--ok)",
        warn: "var(--warn)",
        bad:  "var(--bad)",
        info: "var(--info)",

        "tier-read":  "var(--tier-read)",
        "tier-write": "var(--tier-write)",
        "tier-destr": "var(--tier-destr)",

        "ag-shader":   "var(--ag-shader)",
        "ag-ui":       "var(--ag-ui)",
        "ag-dots":     "var(--ag-dots)",
        "ag-perf":     "var(--ag-perf)",
        "ag-gameplay": "var(--ag-gameplay)",
        "ag-unity":    "var(--ag-unity)",
        "ag-systems":  "var(--ag-systems)",
        "ag-techart":  "var(--ag-techart)",
        "ag-addr":     "var(--ag-addr)",
        "ag-qa":       "var(--ag-qa)",
      },

      fontFamily: {
        body: ["Inter", "system-ui", "sans-serif"],
        mono: ["JetBrains Mono", "ui-monospace", "monospace"],
        hud:  ["Orbitron", "Inter", "sans-serif"],
      },

      borderRadius: {
        "r-1": "var(--r-1)",
        "r-2": "var(--r-2)",
        "r-3": "var(--r-3)",
        "r-4": "var(--r-4)",
        "r-5": "var(--r-5)",
      },

      boxShadow: {
        "elev-1": "var(--shadow-1)",
        "elev-2": "var(--shadow-2)",
        "elev-3": "var(--shadow-3)",
        "glow-brand": "var(--shadow-glow-brand)",
      },

      backgroundImage: {
        "grad-brand":   "linear-gradient(135deg, #7B5CFF 0%, #4CC9FF 100%)",
        "grad-brand-r": "linear-gradient(110deg, #9D7CFF 0%, #4CC9FF 60%, #7AD8FF 100%)",
      },

      keyframes: {
        "pulse-soft": {
          "0%, 100%": { opacity: "1", transform: "scale(1)" },
          "50%":      { opacity: "0.55", transform: "scale(0.85)" },
        },
      },

      animation: {
        "pulse-soft": "pulse-soft 1.2s cubic-bezier(0.65, 0, 0.35, 1) infinite",
      },

      transitionTimingFunction: {
        "ease-out-soft": "cubic-bezier(0.16, 1, 0.3, 1)",
        snap:            "cubic-bezier(0.34, 1.56, 0.64, 1)",
      },
    },
  },
  plugins: [],
};
