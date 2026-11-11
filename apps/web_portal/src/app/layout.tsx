import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "EECC — Église Évangélique du Congo Central",
  description: "Portail officiel de l'EECC. Cultes en direct, replays, prédications, podcasts bibliques et communauté chrétienne.",
  icons: {
    icon: "/favicon.ico",
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
      <body className="antialiased min-h-screen flex flex-col bg-background text-textMain">
        {children}
      </body>
    </html>
  );
}
