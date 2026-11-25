import React from 'react';
import { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Actualités & Annonces — EECC',
  description: 'Retrouvez toutes les actualités, séminaires et événements de l\'Église EECC.',
};

interface NewsItem {
  id: string;
  title: string;
  date: string;
  category: string;
  summary: string;
}

const newsList: NewsItem[] = [
  {
    id: 'convention-annuelle-2026',
    title: 'Grande Convention Annuelle : Vivre dans la Plénitude de Christ',
    date: '15 Décembre 2026',
    category: 'Événement Spécial',
    summary: 'Trois jours exceptionnels d\'impact spirituel, de louange et de prédication prophétique au Siège Central.',
  },
  {
    id: 'lancement-eecc-tv',
    title: 'Lancement Officiel de l\'Écosystème EECC TV & EECC Caméra',
    date: '28 Novembre 2026',
    category: 'Média & Diffusion',
    summary: 'La régie broadcast native et l\'application mobile TV sont désormais actives pour diffuser la gloire de Dieu partout dans le monde.',
  },
  {
    id: 'baptemes-immersion',
    title: 'Cérémonie Solennelle des Baptêmes d\'Immersion',
    date: '5 Novembre 2026',
    category: 'Vie de l\'Église',
    summary: 'Plus de 45 nouveaux convertis ont confessé publiquement leur foi en Jésus-Christ lors de la cérémonie au fleuve.',
  },
];

export default function ActualitesPage() {
  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 py-16 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto space-y-10">
        <div className="text-center space-y-3">
          <span className="inline-block px-3 py-1 bg-purple-600/20 text-purple-400 rounded-full text-xs font-semibold uppercase tracking-wider">
            Vie de l'Église
          </span>
          <h1 className="text-4xl sm:text-5xl font-black text-white tracking-tight">
            Actualités &amp; Événements
          </h1>
          <p className="text-slate-300 max-w-xl mx-auto">
            Restez informés des temps forts, séminaires et activités spirituelles de la communauté EECC.
          </p>
        </div>

        <div className="space-y-6">
          {newsList.map((item) => (
            <article
              key={item.id}
              className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-6 sm:p-8 hover:border-purple-500/50 transition-colors space-y-3"
            >
              <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                <span className="px-2.5 py-0.5 bg-purple-600/20 text-purple-400 rounded font-medium">
                  {item.category}
                </span>
                <time className="text-slate-400">{item.date}</time>
              </div>
              <h2 className="text-2xl font-bold text-white hover:text-purple-400 transition-colors cursor-pointer">
                {item.title}
              </h2>
              <p className="text-slate-300 text-sm leading-relaxed">{item.summary}</p>
            </article>
          ))}
        </div>
      </div>
    </div>
  );
}
