import React from "react";
import Link from "next/link";
import {
  Calendar,
  Radio,
  FileAudio,
  DollarSign,
  Plus,
  ArrowUpRight,
  ShieldAlert,
  CheckCircle,
  Database,
} from "lucide-react";

export const metadata = {
  title: "Tableau de Bord — Console Admin EECC",
};

export default function AdminDashboardPage() {
  const stats = [
    {
      label: "Direct Régie Windows",
      value: "En attente",
      sub: "Prêt pour le culte de dimanche",
      icon: Radio,
      color: "text-slate-500",
      bg: "bg-slate-100",
    },
    {
      label: "Dons collectés (Mois)",
      value: "4 850 $",
      sub: "+12% vs mois dernier",
      icon: DollarSign,
      color: "text-green-600",
      bg: "bg-green-50",
    },
    {
      label: "Cultes & Replays",
      value: "28 cultes",
      sub: "YouTube • Facebook • TikTok",
      icon: Calendar,
      color: "text-primary",
      bg: "bg-primary/10",
    },
    {
      label: "Podcasts & Audios",
      value: "42 sermons",
      sub: "Stockés sur Supabase Storage",
      icon: FileAudio,
      color: "text-indigo-600",
      bg: "bg-indigo-50",
    },
  ];

  const recentTransactions = [
    { id: "DON-001", donor: "Fidèle Anonyme", amount: "50 $", method: "Airtel Money", status: "Confirmé", date: "Aujourd'hui, 11:20" },
    { id: "DON-002", donor: "M. Kankonde", amount: "100 $", method: "M-Pesa", status: "Confirmé", date: "Hier, 16:45" },
    { id: "DON-003", donor: "Mme Kabila", amount: "25 $", method: "Orange Money", status: "Confirmé", date: "Hier, 09:12" },
  ];

  return (
    <div className="space-y-8">
      {/* Titre & Actions Rapides */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-textMain tracking-tight">Tableau de Bord Général</h1>
          <p className="text-xs text-slate-500 mt-1">Supervision de l’écosystème numérique, des diffusions et de la trésorerie.</p>
        </div>

        <div className="flex items-center space-x-3">
          <Link
            href="/admin/cultes"
            className="inline-flex items-center px-4 py-2 rounded-xl text-xs font-bold text-white bg-primary hover:bg-primary-dark transition-colors shadow-sm"
          >
            <Plus className="w-3.5 h-3.5 mr-1.5" />
            Programmer un culte
          </Link>
          <Link
            href="/admin/tresorerie"
            className="inline-flex items-center px-4 py-2 rounded-xl text-xs font-bold text-slate-700 bg-white border border-slate-200 hover:bg-slate-50 transition-colors"
          >
            Voir la Trésorerie
          </Link>
        </div>
      </div>

      {/* Cartes Métriques */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        {stats.map((st, i) => {
          const Icon = st.icon;
          return (
            <div key={i} className="bg-white rounded-2xl p-5 border border-slate-200/80 shadow-sm">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-semibold text-slate-500">{st.label}</span>
                <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${st.bg} ${st.color}`}>
                  <Icon className="w-4 h-4" />
                </div>
              </div>
              <div className="text-2xl font-extrabold text-textMain mb-1">{st.value}</div>
              <div className="text-[11px] text-slate-400">{st.sub}</div>
            </div>
          );
        })}
      </div>

      {/* Deux Colonnes : Prochains Cultes & Activité Trésorerie */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Colonne 1 & 2 : Dernières Transactions Maishapay */}
        <div className="lg:col-span-2 bg-white rounded-2xl border border-slate-200/80 p-6 shadow-sm">
          <div className="flex items-center justify-between mb-5">
            <div>
              <h3 className="font-bold text-textMain text-base">Dons Récents (Passerelle Maishapay)</h3>
              <p className="text-xs text-slate-500">Collectes directes en ligne et mobile money</p>
            </div>
            <Link href="/admin/tresorerie" className="text-xs font-bold text-primary hover:underline flex items-center">
              Voir tout <ArrowUpRight className="w-3.5 h-3.5 ml-1" />
            </Link>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 text-slate-500 font-semibold border-y border-slate-200">
                <tr>
                  <th className="py-2.5 px-3">Réf.</th>
                  <th className="py-2.5 px-3">Donateur</th>
                  <th className="py-2.5 px-3">Montant</th>
                  <th className="py-2.5 px-3">Moyen</th>
                  <th className="py-2.5 px-3">Statut</th>
                  <th className="py-2.5 px-3">Date</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {recentTransactions.map((tx) => (
                  <tr key={tx.id} className="hover:bg-slate-50/80">
                    <td className="py-3 px-3 font-mono font-medium text-slate-600">{tx.id}</td>
                    <td className="py-3 px-3 font-semibold text-textMain">{tx.donor}</td>
                    <td className="py-3 px-3 font-bold text-green-600">{tx.amount}</td>
                    <td className="py-3 px-3 text-slate-500">{tx.method}</td>
                    <td className="py-3 px-3">
                      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-50 text-green-700">
                        <CheckCircle className="w-2.5 h-2.5 mr-1" /> {tx.status}
                      </span>
                    </td>
                    <td className="py-3 px-3 text-slate-400">{tx.date}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Colonne 3 : Sécurité & Connexions Services */}
        <div className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-sm space-y-5">
          <h3 className="font-bold text-textMain text-base">État des Intégrations</h3>

          <div className="space-y-3.5">
            <div className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-200/60">
              <div>
                <strong className="block text-xs text-textMain font-bold">Supabase Backend</strong>
                <span className="text-[10px] text-slate-500">BDD & Authentification</span>
              </div>
              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-green-100 text-green-700">Connecté</span>
            </div>

            <div className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-200/60">
              <div>
                <strong className="block text-xs text-textMain font-bold">Supabase Storage</strong>
                <span className="text-[10px] text-slate-500">Buckets: podcasts, documents</span>
              </div>
              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-green-100 text-green-700">Actif</span>
            </div>

            <div className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-200/60">
              <div>
                <strong className="block text-xs text-textMain font-bold">Maishapay API</strong>
                <span className="text-[10px] text-slate-500">Passerelle de paiement RDC</span>
              </div>
              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-green-100 text-green-700">Prêt</span>
            </div>

            <div className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-200/60">
              <div>
                <strong className="block text-xs text-textMain font-bold">Brevo E-mails (OTP)</strong>
                <span className="text-[10px] text-slate-500">Codes 5 car. Trésorerie</span>
              </div>
              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-green-100 text-green-700">Configuré</span>
            </div>
          </div>

          <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-900 text-xs">
            <div className="flex items-center space-x-1.5 font-bold mb-1">
              <ShieldAlert className="w-4 h-4 text-amber-600" />
              <span>Règle de Sécurité Retrait</span>
            </div>
            Tout retrait de fonds de la trésorerie exige la double confirmation avec le code OTP 5 caractères expédié par Brevo.
          </div>
        </div>
      </div>
    </div>
  );
}
