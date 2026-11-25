import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "EECC — Église Évangélique les Cohéritiers du Christ",
  description:
    "Portail officiel : cultes en direct, replays, prédications, podcasts, communauté, dons.",
  icons: {
    icon: "/icon-64.png",
    apple: "/apple-touch-icon.png",
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="fr" className="scroll-smooth">
      <body
        className="
          antialiased
          min-h-screen
          bg-background
          text-textMain
          font-sans
          antialiased
          overflow-x-hidden
        "
      >
        {children}
      </body>
    </html>
  );
}
