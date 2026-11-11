/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        background: "#F8FAFC",
        surface: "#FFFFFF",
        panel: "#F1F5F9",
        border: "#CBD5E1",
        primary: {
          DEFAULT: "#1E3A8A", // 20% Bleu-Gris institutionnel
          light: "#3B82F6",
          dark: "#172554",
        },
        slate: {
          50: "#F8FAFC",
          100: "#F1F5F9",
          200: "#E2E8F0",
          300: "#CBD5E1",
          400: "#94A3B8",
          500: "#64748B",
          600: "#475569",
          700: "#334155",
          800: "#1E293B",
          900: "#0F172A", // 10% Noir
        },
        live: "#DC2626", // Rouge en direct
        preview: "#16A34A", // Vert aperçu
        warning: "#EA580C",
      },
    },
  },
  plugins: [],
};
