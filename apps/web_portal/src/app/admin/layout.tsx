"use client";

import React from "react";
import Link from "next/link";
import {
  Church,
  LayoutDashboard,
  Calendar,
  Radio,
  FileText,
  DollarSign,
  Users,
  Settings,
  ExternalLink,
} from "lucide-react";

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const adminNav = [
    { label: "Tableau de bord", href: "/admin", icon: LayoutDashboard },
    { label: "Cultes & Directs", href: "/admin/cultes", icon: Calendar },
    { label: "Média & Podcasts", href: "/admin/medias", icon: FileText },
    { label: "Trésorerie & Dons", href: "/admin/tresorerie", icon: DollarSign },
    { label: "Utilisateurs & Rôles", href: "/admin/utilisateurs", icon: Users },
    { label: "Paramètres", href: "/admin/parametres", icon: Settings },
  ];

  return (
    <div className="min-h-screen bg-slate-100 flex">
      {/* Barre Latérale Admin */}
      <aside className="w-64 bg-slate-900 text-white flex flex-col justify-between hidden md:flex">
        <div>
          {/* Logo & Titre Console */}
          <div className="h-20 flex items-center px-6 border-b border-slate-800 space-x-3">
            <div className="w-9 h-9 rounded-lg bg-primary flex items-center justify-center text-white">
              <Church className="w-5 h-5" />
            </div>
            <div>
              <span className="font-bold text-sm tracking-tight block">Console EECC</span>
              <span className="text-[10px] text-slate-400 font-medium">Administration & Régie</span>
            </div>
          </div>

          {/* Navigation */}
          <nav className="p-4 space-y-1">
            {adminNav.map((item) => {
              const Icon = item.icon;
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className="flex items-center px-3 py-2.5 rounded-xl text-xs font-semibold text-slate-300 hover:text-white hover:bg-slate-800 transition-colors"
                >
                  <Icon className="w-4 h-4 mr-3 text-slate-400" />
                  {item.label}
                </Link>
              );
            })}
          </nav>
        </div>

        {/* Pied de la barre latérale */}
        <div className="p-4 border-t border-slate-800">
          <Link
            href="/"
            className="flex items-center justify-between px-3 py-2 rounded-xl text-xs text-slate-400 hover:text-white hover:bg-slate-800 transition-colors"
          >
            <span>Retour au site public</span>
            <ExternalLink className="w-3.5 h-3.5" />
          </Link>
        </div>
      </aside>

      {/* Contenu Principal */}
      <div className="flex-1 flex flex-col">
        {/* Header Admin */}
        <header className="h-16 bg-white border-b border-slate-200 px-6 flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <span className="w-2.5 h-2.5 rounded-full bg-green-500" />
            <span className="text-xs font-semibold text-slate-600">Système opérationnel (Supabase + Maishapay)</span>
          </div>

          <div className="flex items-center space-x-4">
            <div className="text-right">
              <span className="block text-xs font-bold text-textMain">Administrateur Église</span>
              <span className="text-[10px] text-slate-500">Rôle : Admin Général</span>
            </div>
            <div className="w-9 h-9 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold text-xs">
              AD
            </div>
          </div>
        </header>

        {/* Page Content */}
        <main className="flex-1 p-6 md:p-8 overflow-y-auto">
          {children}
        </main>
      </div>
    </div>
  );
}
