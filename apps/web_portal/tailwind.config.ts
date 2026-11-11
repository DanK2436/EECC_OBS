import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        background: "#F8FAFC",
        surface: "#FFFFFF",
        primary: {
          DEFAULT: "#1E3A8A", // 20% Bleu-Gris institutionnel
          light: "#3B82F6",
          dark: "#172554",
        },
        slate: {
          light: "#94A3B8",
          DEFAULT: "#475569",
          dark: "#334155",
        },
        textMain: "#0F172A", // 10% Noir / Texte
        live: "#DC2626", // Rouge en direct
        warning: "#F59E0B",
        success: "#10B981",
      },
    },
  },
  plugins: [],
};
export default config;
