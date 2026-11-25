"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { supabase } from "@/lib/supabaseClient";

export default function AdminDashboardPage() {
  const [stats, setStats] = useState(null);
  const [recentDonations, setRecentDonations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadData() {
      setLoading(true);
      try {
        // 1. Statistiques
        const [donCount, confirmedRes] = await Promise.all([
          supabase.from("donations").select("*", { count: "exact", head: true }),
          supabase
            .from("donations")
            .select("amount, currency")
            .eq("status", "confirmed")
            .order("created_at", { ascending: false })
        ]);

        const totalDonations = donCount || 0;
        const confirmed = confirmedRes.data || [];
        const totalAmount = confirmed.reduce((s, d) => s + (d.amount || 0), 0);

        // 2. Dons récents (5 derniers)
        const { data: recent, error: rError } = await supabase
          .from("donations")
          .select("id, reference, donor_name, donor_email, amount, currency, method, status, created_at")
          .order("created_at", { ascending: false })
          .limit(5);

        if (rError) throw rError;

        setStats({
          totalDonations,
          totalAmount,
          confirmedCount: confirmed.length,
        });
        setRecentDonations(recent || []);
        setLoading(false);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Erreur inconnue");
        setLoading(false);
      }
    }

    loadData();
  }, []);

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <div className="flex items-center gap-2">
          <RefreshCw className="w-5 h-5 animate-spin text-primary" />
          <span className="text-sm text-slate-500">Chargement des données…</span>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50 p-6">
        <div className="max-w-md bg-white rounded-2xl shadow-lg p-8 border border-red-200/50">
          <ShieldAlert className="w-6 h-6 text-red-600 mb-3" />
          <h2 className="text-lg font-bold text-textMain mb-2">Erreur de chargement</h2>
          <p className="text-sm text-slate-600 mb-6">{error}</p>
          <button
            onClick={() => window.location.reload()}
            className="w-full py-2 rounded-lg bg-primary text-white font-medium hover:bg-primary-dark transition-colors"
          >
            Réessayer
          </button>
        </div>
      </div>
    );
  }

  if (!stats || recentDonations.length === 0) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <p className="text-sm text-slate-500">Aucune donnée disponible</p>
      </div>
    );
  }

  const totalDonations = stats.totalDonations;
  const totalAmount = stats.totalAmount;
  const confirmedCount = stats.confirmedCount;

  // Enrichir les dons récents avec affichage
  const enriched = recentDonations.map((tx) => ({
    id: tx.reference,
    donor: tx.donor_name || tx.donor_email || "Donateur anonyme",
    amount: `${tx.amount} ${tx.currency}`,
    method: tx.method.charAt(0).toUpperCase() + tx.method.slice(1),
    status: tx.status,
    date: new Date(tx.created_at).toLocaleDateString("fr-FR", {
      day: "numeric",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
    }),
  }));

  // Couleurs Église EECC : anthracite (gris foncé), bleu-gris, noir
  const primary = "#2d3748"; // anthracite / charcoal
  const secondary = "#4a5568"; // bleu-gris
  const accent = "#1a202c"; // noir de profondeur
  const surface = "#f7fafc";
  const card = "#ffffff";
  const border = "#e2e8f0";
  const success = "#38a169";
  const warning = "#d69e2e";

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      {/* Header */}
      <header className="border-b border-slate-200">
        <div className="max-w-7xl mx-auto px-6 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              {/* Logo Église */}
              <div className="w-10 h-10 rounded-xl bg-primary flex items-center justify-center flex-shrink-0">
                <svg
                  width="24"
                  height="24"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  className="text-[20px] text-white"
                >
                  <path d="M12 2L2 7l10 5 10-5-10-5z" />
                  <path d="M2 7l10 5 10-5" />
                  <path d="M2 12l10 5 10-5" />
                  <path d="M2 17l10 5 10-5" />
                </svg>
              </div>
              <div>
                <span className="font-bold text-xl tracking-tight">EECC</span>
                <span className="text-sm text-slate-500 font-medium">Église Évangélique</span>
              </div>
            </div>

            <nav className="hidden md:block">
              <ul className="flex gap-6 text-sm text-slate-500">
                <li><a href="/admin" className="hover:text-slate-900">Tableau de bord</a></li>
                <li><a href="/admin/cultes" className="hover:text-slate-900">Cultes</a></li>
                <li><a href="/admin/medias" className="hover:text-slate-900">Médias</a></li>
                <li><a href="/admin/tresorerie" className="hover:text-slate-900">Trésorerie</a></li>
                <li><a href="/admin/utilisateurs" className="hover:text-slate-900">Utilisateurs</a></li>
              </ul>
            </nav>
          </div>

          {/* Actions utilisateur */}
          <div className="flex items-center gap-3">
            <span className="text-sm text-slate-500">
              Connecté en tant qu'
            </span>
            <span className="font-medium text-slate-900">Administrateur</span>
            <a
              href="/admin/logout"
              className="ml-2 text-primary underline underline-offset-2 hover:underline-opacity-50"
            >
              Déconnexion
            </a>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-6 py-6">
        {/* Stats Header */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          <div>
            <div className="p-4 rounded-xl bg-primary/5 border border-primary/10">
              <div className="text-2xl font-bold text-primary">{confirmedCount}</div>
              <div className="text-xs text-slate-500 mt-1">Dons confirmés</div>
            </div>
          </div>
          <div>
            <div className="p-4 rounded-xl bg-secondary/5 border border-secondary/10">
              <div className="text-2xl font-bold text-secondary">{totalDonations}</div>
              <div className="text-xs text-slate-500 mt-1">Dons total</div>
            </div>
          </div>
          <div>
            <div className="p-4 rounded-xl bg-accent/5 border border-accent/10">
              <div className="text-2xl font-bold text-accent">{totalAmount > 0 ? totalAmount.toLocaleString() + " $" : "0 $"}</div>
              <div className="text-xs text-slate-500 mt-1">Montant total $</div>
            </div>
          </div>
          <div>
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200">
              <div className="text-2xl font-bold">—</div>
              <div className="text-xs text-slate-500 mt-1">Podcasts & Audios</div>
            </div>
          </div>
        </div>

        {/* Cartes de don récent */}
        <div className="bg-white rounded-2xl overflow-hidden border border-slate-200 mb-8">
          <div className="px-6 py-4 border-b border-slate-200">
            <h2 className="text-lg font-bold text-slate-900">
              <svg
                width="18"
                height="18"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M12 2L2 7l10 5 10-5-10-5z" />
                <path d="M2 7l10 5 10-5" />
                <path d="M2 12l10 5 10-5" />
                <path d="M2 17l10 5 10-5" />
              </svg>
              Dons récents
            </h2>
          </div>
          <div className="p-4 space-y-3">
            {enriched.map((d) => (
              <div key={d.id} className="flex items-center justify-between py-2 border-b border-slate-100 last:border-0">
                <div className="flex items-center gap-3">
                  <span className="text-sm font-medium text-slate-700">{d.donor}</span>
                  <span className="text-xs text-slate-500">{d.date}</span>
                </div>
                <div className="text-right">
                  <span className="text-lg font-medium text-primary">
                    {d.amount}
                  </span>
                  <span className="text-xs text-slate-400">{d.method}</span>
                </div>
              </div>
            ))}
            {enriched.length < 5 ? (
              <p className="text-xs text-slate-400 text-center pt-2">
                {enriched.length} don{"s" if enriched.length > 1 else ""} sur 5 affichés
              </p>
            ) : null}
          </div>
        </div>

        {/* Intégrations */}
        <div className="grid grid-cols-2 gap-6 mb-8">
          <div>
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-xl bg-primary/5 flex items-center justify-center">
                  <svg
                    width="20"
                    height="20"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M12 2L2 7l10 5 10-5-10-5z" />
                    <path d="M2 7l10 5 10-5" />
                  </svg>
                </div>
                <div>
                  <div className="font-medium text-slate-900">Supabase Backend</div>
                  <div className="text-xs text-slate-500">Base de données & Authentification</div>
                </div>
              </div>
            </div>
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 pt-0">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-xl bg-secondary/5 flex items-center justify-center">
                  <svg
                    width="20"
                    height="20"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <rect x="3" y="4" width="18" height="16" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="14" />
                    <line x1="8" y1="2" x2="8" y2="14" />
                  </svg>
                </div>
                <div>
                  <div className="font-medium text-slate-900">Supabase Storage</div>
                  <div className="text-xs text-slate-500">Buckets: podcasts, documents</div>
                </div>
              </div>
            </div>
          </div>

          <div>
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-xl bg-primary/5 flex items-center justify-center">
                  <svg
                    width="20"
                    height="20"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M12 2L2 7l10 5 10-5-10-5z" />
                    <path d="M2 4l16 8-16 8z" />
                    <line x1="9" y1="4" x2="9.01" y2="4" />
                    <line x1="15" y1="4" x2="15.01" y2="4" />
                  </svg>
                </div>
                <div>
                  <div className="font-medium text-slate-900">Maishapay API</div>
                  <div className="text-xs text-slate-500">Passerelle de paiement RDC</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Sécurité retrait */}
        <div className="bg-white rounded-2xl p-6 border border-slate-200 mb-8">
          <h3 className="font-bold text-slate-900 mb-3">
            <svg
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
            Règle de sécurité retrait
          </h3>
          <p className="text-sm text-slate-500">
            Tout retrait de fonds de la trésorerie exige la double confirmation avec le code OTP 5 caractères expédié par Brevo.
          </p>
        </div>
      </main>
    </div>
  );
}
