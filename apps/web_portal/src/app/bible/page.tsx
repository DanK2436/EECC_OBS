'use client';

import React, { useState } from 'react';

const sampleVerses = [
  { book: 'Jean', chapter: 3, verse: 16, text: 'Car Dieu a tant aimé le monde qu\'il a donné son Fils unique, afin que quiconque croit en lui ne périsse point, mais qu\'il ait la vie éternelle.' },
  { book: 'Romains', chapter: 8, verse: 17, text: 'Or, si nous sommes enfants, nous sommes aussi héritiers : héritiers de Dieu, et cohéritiers de Christ, si toutefois nous souffrons avec lui, afin d\'être glorifiés avec lui.' },
  { book: 'Psaumes', chapter: 23, verse: 1, text: 'L\'Éternel est mon berger : je ne manquerai de rien.' },
  { book: 'Ésaïe', chapter: 40, verse: 31, text: 'Mais ceux qui se confient en l\'Éternel renouvellent leur force. Ils prennent le vol comme les aigles ; ils courent, et ne se lassent point ; ils marchent, et ne se fatiguent point.' },
  { book: 'Philippiens', chapter: 4, verse: 13, text: 'Je puis tout par celui qui me fortifie.' },
];

export default function BiblePage() {
  const [search, setSearch] = useState('');

  const filtered = sampleVerses.filter(
    (v) =>
      v.text.toLowerCase().includes(search.toLowerCase()) ||
      v.book.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 py-16 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto space-y-10">
        <div className="text-center space-y-3">
          <span className="inline-block px-3 py-1 bg-amber-600/20 text-amber-400 rounded-full text-xs font-semibold uppercase tracking-wider">
            La Sainte Bible
          </span>
          <h1 className="text-4xl sm:text-5xl font-black text-white tracking-tight">
            Bible en Ligne — Louis Segond 1910
          </h1>
          <p className="text-slate-300 max-w-xl mx-auto">
            Méditez la Parole de Dieu quotidiennement, recherchez des passages bibliques et nourrissez votre foi.
          </p>
        </div>

        {/* Barre de Recherche */}
        <div className="relative">
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Rechercher un livre, un mot clé ou un verset (ex: Jean, berger, héritiers)..."
            className="w-full bg-slate-800 border border-slate-700 rounded-2xl px-5 py-3.5 text-white placeholder-slate-400 focus:outline-none focus:border-amber-500 shadow-xl"
          />
        </div>

        {/* Liste des Versets */}
        <div className="space-y-4">
          {filtered.map((v, idx) => (
            <div
              key={idx}
              className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-6 hover:border-amber-500/40 transition-colors space-y-2"
            >
              <div className="flex items-center justify-between text-amber-400 font-bold text-sm">
                <span>{v.book} {v.chapter}:{v.verse}</span>
                <span className="text-xs text-slate-400">LSG 1910</span>
              </div>
              <p className="text-slate-200 text-base italic leading-relaxed">
                « {v.text} »
              </p>
            </div>
          ))}
          {filtered.length === 0 && (
            <div className="text-center py-12 text-slate-400">
              Aucun verset ne correspond à votre recherche.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
