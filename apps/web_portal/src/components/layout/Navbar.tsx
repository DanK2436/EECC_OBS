"use client";

import React, { useState } from "react";
import Link from "next/link";
import { Menu, X, Play, Heart, Church } from "lucide-react";

export const Navbar = () => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const navLinks = [
    { label: "Accueil", href: "/" },
    { label: "À propos", href: "/a-propos" },
    { label: "Nos églises", href: "/eglises" },
    { label: "Cultes", href: "/cultes" },
    { label: "Actualités", href: "/actualites" },
    { label: "Médiathèque", href: "/mediatheque" },
    { label: "Faire un don", href: "/don" },
    { label: "Contact", href: "/contact" },
  ];

  return (
    <header
      className="sticky top-0 z-50 bg-slate-950/90 backdrop-blur-md border-b border-slate-800/50"
    >
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-20">
          {/* Logo & Nom */}
          <Link href="/" className="flex items-center space-x-3 group">
            <div className="w-10 h-10 rounded-xl bg-slate-900 flex items-center justify-center flex-shrink-0">
              <img
                src="/icon-64.png"
                alt="Logo EECC"
                className="w-6 h-6 text-slate-300"
              />
            </div>
            <div>
              <span className="font-bold text-xl tracking-tight">EECC</span>
              <span className="block text-xs text-slate-400 font-medium">Église Évangélique</span>
            </div>
          </Link>

          {/* Navigation Bureau */}
          <nav className="hidden lg:flex items-center space-x-7">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-sm font-medium text-slate-300 hover:text-slate-200 transition-colors"
              >
                {link.label}
              </Link>
            ))}
          </nav>

          {/* Boutons d'action Bureau */}
          <div className="hidden lg:flex items-center space-x-4">
            <Link
              href="/don"
              className="inline-flex items-center px-4 py-2 rounded-xl text-xs font-bold text-slate-200 hover:text-slate-100 transition-colors"
            >
              <Heart className="w-4 h-4 text-slate-300" />
              Soutenir
            </Link>
            <Link
              href="/cultes"
              className="inline-flex items-center px-5 py-2.5 rounded-xl text-sm font-bold text-white bg-slate-900 hover:bg-slate-800 transition-all shadow-md shadow-slate/25 hover:shadow-lg"
            >
              <Play className="w-4 h-4 mr-2 fill-current" />
              Regarder le direct
            </Link>
          </div>

          {/* Bouton Menu Mobile */}
          <div className="lg:hidden flex items-center space-x-2">
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="p-2 rounded-lg text-slate-300 hover:text-slate-200 hover:bg-slate-900/30"
              aria-label="Menu de navigation"
            >
              {mobileMenuOpen ? <X className="w-6 h-6 text-slate-200" /> : <Menu className="w-6 h-6 text-slate-300" />}
            </button>
          </div>
        </div>
      </div>

      {/* Menu Latéral Mobile */}
      {mobileMenuOpen && (
        <div
          className="lg:hidden border-t border-slate-800/50 bg-slate-950/90 px-4 pt-3 pb-6 space-y-3"
        >
          <div className="flex flex-col space-y-2">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMobileMenuOpen(false)}
                className="px-3 py-2 rounded-lg text-base font-semibold text-slate-300 hover:text-slate-200 hover:bg-slate-900/30 transition-colors"
              >
                {link.label}
              </Link>
            ))}
          </div>
          <div className="pt-4 border-t border-slate-800/50 flex flex-col space-y-2">
            <Link
              href="/cultes"
              onClick={() => setMobileMenuOpen(false)}
              className="w-full flex items-center justify-center px-4 py-3 rounded-xl text-sm font-bold text-white bg-slate-900"
            >
              <Play className="w-4 h-4 mr-2 fill-current" />
              Regarder le direct
            </Link>
            <Link
              href="/don"
              onClick={() => setMobileMenuOpen(false)}
              className="w-full flex items-center justify-center px-4 py-2.5 rounded-xl text-sm font-bold text-primary bg-slate-900/10"
            >
              <Heart className="w-4 h-4 mr-2 text-primary" />
              Faire un don
            </Link>
          </div>
        </div>
      )}
    </header>
  );
};