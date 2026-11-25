import React from 'react';
import { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'À propos — Église Évangélique les Cohéritiers du Christ (EECC)',
  description: 'Histoire, vision pastorale, mission spirituelle et confession de foi de l\'EECC.',
};

export default function AProposPage() {
  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 py-16 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto space-y-12">
        {/* En-Tête */}
        <div className="text-center space-y-4">
          <span className="inline-block px-3 py-1 bg-blue-600/20 text-blue-400 rounded-full text-xs font-semibold uppercase tracking-wider">
            Qui sommes-nous ?
          </span>
          <h1 className="text-4xl sm:text-5xl font-black text-white tracking-tight">
            Église Évangélique les Cohéritiers du Christ
          </h1>
          <p className="text-lg text-slate-300 max-w-2xl mx-auto">
            « Si nous sommes enfants, nous sommes aussi héritiers : héritiers de Dieu, et cohéritiers de Christ. » — Romains 8:17
          </p>
        </div>

        {/* Section 1 : Notre Histoire */}
        <section className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-8 backdrop-blur-sm space-y-4">
          <h2 className="text-2xl font-bold text-blue-400">Notre Histoire &amp; Genèse</h2>
          <p className="text-slate-300 leading-relaxed">
            Fondée sous la conduite du Saint-Esprit, l'Église Évangélique les Cohéritiers du Christ (EECC) a pour vocation de proclamer l'Évangile intégral de Jésus-Christ. Depuis ses débuts, notre communauté rassemble des croyants passionnés par la Parole de Dieu, engagés dans la prière fervente, l'évangélisation et l'amour fraternel.
          </p>
        </section>

        {/* Section 2 : Vision & Mission */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-8 space-y-3">
            <h3 className="text-xl font-bold text-amber-400">Notre Vision</h3>
            <p className="text-slate-300 text-sm leading-relaxed">
              Bâtir une église dynamique, sainte et prophétique, formant des disciples matures capables d'impacter leur génération par la puissance de l'Évangile et de manifester le règne de Dieu sur terre.
            </p>
          </div>
          <div className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-8 space-y-3">
            <h3 className="text-xl font-bold text-emerald-400">Notre Mission</h3>
            <p className="text-slate-300 text-sm leading-relaxed">
              Prêcher le salut par grâce, équiper les saints pour le ministère, soutenir les veuves et orphelins, implanter des églises locales et diffuser la Parole de Dieu à travers les médias modernes et la régie audiovisuelle.
            </p>
          </div>
        </div>

        {/* Section 3 : Confession de Foi */}
        <section className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-8 space-y-4">
          <h2 className="text-2xl font-bold text-blue-400">Confession de Foi Fondamentale</h2>
          <ul className="space-y-3 text-slate-300 text-sm">
            <li className="flex items-start gap-3">
              <span className="text-blue-400 font-bold">1.</span>
              <span><strong>La Bible :</strong> Inspirée de Dieu, infaillible et souveraine règle de notre foi et de notre conduite.</span>
            </li>
            <li className="flex items-start gap-3">
              <span className="text-blue-400 font-bold">2.</span>
              <span><strong>Le Dieu Trinité :</strong> Un seul Dieu vivant et vrai, éternellement existant en trois personnes : Père, Fils et Saint-Esprit.</span>
            </li>
            <li className="flex items-start gap-3">
              <span className="text-blue-400 font-bold">3.</span>
              <span><strong>Le Salut :</strong> Obtenu par la seule grâce, par le moyen de la foi dans le sacrifice expiatoire de Jésus-Christ à la croix.</span>
            </li>
            <li className="flex items-start gap-3">
              <span className="text-blue-400 font-bold">4.</span>
              <span><strong>Le Saint-Esprit :</strong> Sa présence sanctifiante, le baptême dans le Saint-Esprit et l'exercice des dons spirituels pour l'édification du corps de Christ.</span>
            </li>
          </ul>
        </section>
      </div>
    </div>
  );
}
