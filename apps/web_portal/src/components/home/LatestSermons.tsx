import React from "react";
import Link from "next/link";
import { Headphones, ExternalLink, Calendar, User, ArrowRight } from "lucide-react";

export const LatestSermons = () => {
  const sermons = [
    {
      title: "La Puissance de la Foi Agissante",
      speaker: "Pasteur Principal",
      date: "08 Novembre 2026",
      scripture: "Hébreux 11:1-6",
      desc: "Comment activer sa foi face aux défis contemporains et voir la main de Dieu intervenir.",
      podcastUrl: "/mediatheque",
      replayUrl: "https://www.youtube.com",
      platform: "YouTube",
    },
    {
      title: "Bâtir sur le Roc Inébranlable",
      speaker: "Pasteur Associé",
      date: "01 Novembre 2026",
      scripture: "Matthieu 7:24-27",
      desc: "Les fondements indispensables pour que notre vie de famille et notre foi résistent à la tempête.",
      podcastUrl: "/mediatheque",
      replayUrl: "https://www.facebook.com",
      platform: "Facebook",
    },
    {
      title: "La Grâce qui Transforme les Cœurs",
      speaker: "Évangéliste Invité",
      date: "25 Octobre 2026",
      scripture: "Éphésiens 2:8-10",
      desc: "Découvrir la profondeur imméritée de l’amour de Dieu et sa vocation pour chaque croyant.",
      podcastUrl: "/mediatheque",
      replayUrl: "https://www.youtube.com",
      platform: "YouTube",
    },
  ];

  return (
    <section className="py-20 bg-white border-t border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col md:flex-row md:items-end justify-between mb-14">
          <div>
            <span className="text-xs font-bold text-primary uppercase tracking-wider block mb-2">
              Nourriture Spirituelle
            </span>
            <h2 className="text-3xl font-extrabold text-textMain tracking-tight">Dernières Prédications</h2>
            <p className="mt-2 text-base text-slate">
              Retrouvez les messages récents en écoute audio ou en vidéo sur nos plateformes.
            </p>
          </div>
          <Link
            href="/cultes"
            className="mt-4 md:mt-0 inline-flex items-center text-sm font-bold text-primary hover:text-primary-dark transition-colors"
          >
            Tous les cultes & replays <ArrowRight className="w-4 h-4 ml-1.5" />
          </Link>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          {sermsMap(sermons)}
        </div>
      </div>
    </section>
  );
};

function sermsMap(sermons: any[]) {
  return sermons.map((s, idx) => (
    <article
      key={idx}
      className="bg-background rounded-2xl p-6 border border-slate-200/80 shadow-sm hover:shadow-md transition-shadow flex flex-col justify-between"
    >
      <div>
        <div className="flex items-center justify-between text-xs text-slate mb-3">
          <span className="flex items-center">
            <Calendar className="w-3.5 h-3.5 mr-1 text-primary" /> {s.date}
          </span>
          <span className="font-semibold text-primary">{s.scripture}</span>
        </div>

        <h3 className="text-lg font-bold text-textMain mb-2 leading-snug">{s.title}</h3>
        <p className="text-xs text-slate-500 flex items-center mb-4">
          <User className="w-3.5 h-3.5 mr-1" /> {s.speaker}
        </p>
        <p className="text-sm text-slate leading-relaxed mb-6">{s.desc}</p>
      </div>

      <div className="pt-4 border-t border-slate-200 flex items-center justify-between gap-3">
        <Link
          href={s.podcastUrl}
          className="inline-flex items-center text-xs font-bold text-primary bg-primary/10 hover:bg-primary/15 px-3 py-2 rounded-lg transition-colors"
        >
          <Headphones className="w-3.5 h-3.5 mr-1.5" /> Écouter podcast
        </Link>
        <a
          href={s.replayUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center text-xs font-bold text-slate-700 hover:text-textMain px-3 py-2 rounded-lg transition-colors"
        >
          Replay ({s.platform}) <ExternalLink className="w-3 h-3 ml-1" />
        </a>
      </div>
    </article>
  ));
}
