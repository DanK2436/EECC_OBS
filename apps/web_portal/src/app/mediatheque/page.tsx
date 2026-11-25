import React from 'react';
import { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Médiathèque & Podcasts — EECC',
  description: 'Écoutez les prédications en podcast audio, téléchargez les résumés et revivez les cultes en replay.',
};

interface MediaItem {
  id: string;
  title: string;
  speaker: string;
  date: string;
  type: 'video' | 'audio' | 'pdf';
  duration: string;
  url: string;
}

const mediaCatalog: MediaItem[] = [
  {
    id: 'm1',
    title: 'La Puissance de la Résurrection dans le Croyant',
    speaker: 'Pasteur Principal',
    date: '22 Novembre 2026',
    type: 'video',
    duration: '1h 35m',
    url: '/cultes',
  },
  {
    id: 'm2',
    title: 'Marcher par l\'Esprit et non selon la chair',
    speaker: 'Pasteur Associé',
    date: '15 Novembre 2026',
    type: 'audio',
    duration: '52 min (Podcast MP3)',
    url: '#',
  },
  {
    id: 'm3',
    title: 'Guide d\'Étude Biblique : Les Épîtres de Paul',
    speaker: 'Département Éducation Chrétienne',
    date: '10 Novembre 2026',
    type: 'pdf',
    duration: 'PDF 24 Pages',
    url: '#',
  },
];

export default function MediathequePage() {
  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 py-16 px-4 sm:px-6 lg:px-8">
      <div className="max-w-5xl mx-auto space-y-12">
        <div className="text-center space-y-3">
          <span className="inline-block px-3 py-1 bg-blue-600/20 text-blue-400 rounded-full text-xs font-semibold uppercase tracking-wider">
            Ressources Spirituelles
          </span>
          <h1 className="text-4xl sm:text-5xl font-black text-white tracking-tight">
            Médiathèque &amp; Podcasts
          </h1>
          <p className="text-slate-300 max-w-2xl mx-auto">
            Accédez gratuitement à l'ensemble des enseignements, prédications et documents d'édification.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {mediaCatalog.map((item) => (
            <div
              key={item.id}
              className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-6 flex flex-col justify-between space-y-4 hover:border-blue-500/50 transition-colors"
            >
              <div className="space-y-3">
                <span className={`inline-block px-2.5 py-0.5 rounded text-xs font-semibold uppercase ${
                  item.type === 'video' ? 'bg-red-600/20 text-red-400' :
                  item.type === 'audio' ? 'bg-emerald-600/20 text-emerald-400' :
                  'bg-amber-600/20 text-amber-400'
                }`}>
                  {item.type === 'video' ? '📺 Replay Vidéo' : item.type === 'audio' ? '🎙️ Podcast Audio' : '📄 Document PDF'}
                </span>
                <h2 className="text-lg font-bold text-white leading-snug">{item.title}</h2>
                <div className="text-xs text-slate-400 space-y-1">
                  <p>👤 {item.speaker}</p>
                  <p>📅 {item.date}</p>
                  <p>⏱️ {item.duration}</p>
                </div>
              </div>

              <a
                href={item.url}
                className="w-full text-center py-2.5 px-4 bg-blue-600 hover:bg-blue-500 text-white rounded-xl text-xs font-bold transition-colors"
              >
                {item.type === 'video' ? 'Regarder le Replay' : item.type === 'audio' ? 'Écouter le Podcast' : 'Télécharger le PDF'}
              </a>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
