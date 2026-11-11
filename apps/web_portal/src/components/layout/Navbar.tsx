"use client";

import React, { useState } from "react";
import Link from "next/link";
import { Menu, X, Play, Heart, Church } from "lucide-react";

export const Navbar = () => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const navLinks = [
    { label: "Accueil", href: "/" },
    { label: "À propos", href: "/a-propos" },
    { label: "Nos églises", href: "/nos-eglises" },
    { label: "Cultes", href: "/cultes" },
    { label: "Actualités", href: "/actualites" },
    { label: "Médiathèque", href: "/mediatheque" },
    { label: "Faire un don", href: "/don" },
    { label: "Contact", href: "/contact" },
  ];

  return (
    <header className="sticky top-0 z-50 bg-white/95 backdrop-blur-md border-b border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-20">
          {/* Logo & Nom */}
          <Link href="/" className="flex items-center space-x-3 group">
            <div className="w-11 h-11 rounded-xl overflow-hidden shadow-md shadow-primary/20 group-hover:scale-105 transition-transform bg-white border border-slate-200 flex items-center justify-center">
              <img src="/logo.png" alt="Logo EECC" className="w-full h-full object-contain p-0.5" />
            </div>
            <div>
              <span className="text-xl font-bold text-textMain tracking-tight">EECC</span>
              <span className="block text-xs text-slate font-medium">Église Évangélique</span>
            </div>
          </Link>

          {/* Navigation Bureau */}
          <nav className="hidden lg:flex items-center space-x-7">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-sm font-semibold text-slate hover:text-primary transition-colors"
              >
                {link.label}
              </Link>
            ))}
          </nav>

          {/* Boutons d'action Bureau */}
          <div className="hidden lg:flex items-center space-x-4">
            <Link
              href="/don"
              className="inline-flex items-center px-4 py-2 rounded-xl text-sm font-bold text-primary bg-primary/10 hover:bg-primary/15 transition-colors"
            >
              <Heart className="w-4 h-4 mr-2 text-primary" />
              Soutenir
            </Link>
            <Link
              href="/cultes"
              className="inline-flex items-center px-5 py-2.5 rounded-xl text-sm font-bold text-white bg-primary hover:bg-primary-dark transition-all shadow-md shadow-primary/25 hover:shadow-lg"
            >
              <Play className="w-4 h-4 mr-2 fill-current" />
              Regarder le direct
            </Link>
          </div>

          {/* Bouton Menu Mobile */}
          <div className="lg:hidden flex items-center space-x-2">
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="p-2 rounded-lg text-slate hover:text-textMain hover:bg-slate-100"
              aria-label="Menu de navigation"
            >
              {mobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>
      </div>

      {/* Menu Latéral Mobile */}
      {mobileMenuOpen && (
        <div className="lg:hidden border-b border-slate-200 bg-white px-4 pt-3 pb-6 space-y-3">
          <div className="flex flex-col space-y-2">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMobileMenuOpen(false)}
                className="px-3 py-2 rounded-lg text-base font-semibold text-slate hover:text-primary hover:bg-slate-50 transition-colors"
              >
                {link.label}
              </Link>
            ))}
          </div>
          <div className="pt-4 border-t border-slate-100 flex flex-col space-y-2">
            <Link
              href="/cultes"
              onClick={() => setMobileMenuOpen(false)}
              className="w-full flex items-center justify-center px-4 py-3 rounded-xl text-sm font-bold text-white bg-primary"
            >
              <Play className="w-4 h-4 mr-2 fill-current" />
              Regarder le direct
            </Link>
            <Link
              href="/don"
              onClick={() => setMobileMenuOpen(false)}
              className="w-full flex items-center justify-center px-4 py-2.5 rounded-xl text-sm font-bold text-primary bg-primary/10"
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
