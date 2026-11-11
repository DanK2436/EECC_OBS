import React from "react";
import { Navbar } from "@/components/layout/Navbar";
import { Footer } from "@/components/layout/Footer";
import { Play, ExternalLink, Calendar, User, Search, Video } from "lucide-react";

export const metadata = {
  title: "Cultes & Replays — EECC",
  description: "Suivez nos cultes en direct ou retrouvez les replays officiels sur YouTube, Facebook et TikTok.",
};

export default function CultesPage() {
  const replays = [
    {
      id: "rep-1",
      title: "Culte d'Actions de Grâce — Victoire en Christ",
      speaker: "Pasteur Principal",
      date: "08 Novembre 2026",
      platform: "YouTube",
      url: "https://www.youtube.com",
      duration: "1h 45m",
      theme: "La Puissance de la Résurrection",
    },
    {
      id: "rep-2",
      title: "Conférence Annuelle : Bâtir sur le Roc Inébranlable",
      speaker: "Pasteur Associé",
      date: "01 Novembre 2026",
      platform: "Facebook",
      url: "https://www.facebook.com",
      duration: "2h 10m",
      theme: "Les Fondements de la Famille Chrétienne",
    },
    {
      id: "rep-3",
      title: "Moments d'Adoration Vivante & Célébration",
      speaker: "Groupe de Louange EECC",
      date: "25 Octobre 2026",
      platform: "TikTok",
      url: "https://www.tiktok.com",
      duration: "45m",
      theme: "Louange et Prière Prophétique",
    },
  ];

  return (
    <>
      <Navbar />
      <main className="flex-grow py-12">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          {/* En-tête */}
          <div className="max-w-3xl mb-12">
            <span className="text-xs font-bold text-primary uppercase tracking-wider block mb-2">
              Diffusions & Archives
            </span>
            <h1 className="text-4xl font-extrabold text-textMain tracking-tight">
              Cultes en Direct & Replays
            </h1>
            <p className="mt-3 text-base text-slate">
              Regardez le culte en cours ou retrouvez l’ensemble des célébrations passées référencées sur nos chaînes officielles.
            </p>
          </div>

          {/* Section Lecteur Direct */}
          <div className="bg-slate-950 rounded-2xl p-6 sm:p-10 text-white mb-16 shadow-xl border border-slate-800">
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center space-x-3">
                <span className="w-3 h-3 rounded-full bg-slate-500" />
                <span className="text-xs font-bold tracking-wider text-slate-400 uppercase">
                  Signal en attente de direct
                </span>
              </div>
              <span className="text-xs text-slate-500">Flux HLS / EECC Studio</span>
            </div>

            <div className="aspect-video w-full bg-slate-900 rounded-xl flex flex-col items-center justify-center p-8 text-center border border-slate-800">
              <div className="w-16 h-16 rounded-full bg-white/5 flex items-center justify-center mb-4">
                <Video className="w-8 h-8 text-slate-500" />
              </div>
              <h3 className="text-xl font-bold mb-2">Aucun culte en direct en ce moment</h3>
              <p className="text-sm text-slate-400 max-w-md">
                Le prochain direct aura lieu ce <strong>dimanche à 08h30</strong>. Le lecteur démarrera automatiquement au lancement de la régie EECC Studio.
              </p>
            </div>
          </div>

          {/* Catalogue des Replays */}
          <div>
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
              <div>
                <h2 className="text-2xl font-bold text-textMain">Catalogue des Replays</h2>
                <p className="text-xs text-slate mt-1">
                  Les vidéos complètes sont hébergées sur leurs plateformes d’origine respectives.
                </p>
              </div>

              {/* Barre de recherche */}
              <div className="relative w-full sm:w-72">
                <Search className="w-4 h-4 text-slate absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  placeholder="Rechercher un culte, orateur..."
                  className="w-full pl-9 pr-4 py-2 text-sm bg-white rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
              {replays.map((rep) => (
                <div
                  key={rep.id}
                  className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden hover:shadow-md transition-shadow flex flex-col justify-between"
                >
                  <div>
                    {/* Miniature */}
                    <div className="aspect-video bg-slate-100 relative flex items-center justify-center border-b border-slate-100">
                      <Play className="w-12 h-12 text-slate-300" />
                      <div className="absolute top-3 left-3 px-2 py-0.5 rounded text-[10px] font-bold text-white bg-slate-900/80">
                        {rep.platform}
                      </div>
                      <div className="absolute bottom-3 right-3 px-2 py-0.5 rounded text-[10px] font-bold text-white bg-black/70">
                        {rep.duration}
                      </div>
                    </div>

                    <div className="p-5">
                      <div className="flex items-center text-xs text-slate-500 mb-2 space-x-3">
                        <span className="flex items-center">
                          <Calendar className="w-3.5 h-3.5 mr-1" /> {rep.date}
                        </span>
                        <span className="flex items-center">
                          <User className="w-3.5 h-3.5 mr-1" /> {rep.speaker}
                        </span>
                      </div>
                      <h3 className="font-bold text-textMain text-base mb-2 leading-snug">{rep.title}</h3>
                      <p className="text-xs text-slate leading-relaxed">{rep.theme}</p>
                    </div>
                  </div>

                  <div className="p-5 pt-0">
                    <a
                      href={rep.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="w-full inline-flex items-center justify-center px-4 py-2.5 rounded-xl text-xs font-bold text-white bg-primary hover:bg-primary-dark transition-colors"
                    >
                      Regarder sur {rep.platform}
                      <ExternalLink className="w-3.5 h-3.5 ml-1.5" />
                    </a>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </main>
      <Footer />
    </>
  );
}
