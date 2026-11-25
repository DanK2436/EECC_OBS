'use client';

import React, { useState } from 'react';

export default function ContactPage() {
  const [submitted, setSubmitted] = useState(false);
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    phone: '',
    type: 'priere',
    message: '',
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitted(true);
  };

  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 py-16 px-4 sm:px-6 lg:px-8">
      <div className="max-w-3xl mx-auto space-y-10">
        <div className="text-center space-y-3">
          <span className="inline-block px-3 py-1 bg-blue-600/20 text-blue-400 rounded-full text-xs font-semibold uppercase tracking-wider">
            Écoute &amp; Accompagnement
          </span>
          <h1 className="text-4xl sm:text-5xl font-black text-white tracking-tight">
            Contact &amp; Requête de Prière
          </h1>
          <p className="text-slate-300 max-w-xl mx-auto">
            Vous avez besoin de prière, d'un conseil pastoral ou d'une information ? L'équipe pastorale est à votre écoute.
          </p>
        </div>

        <div className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-8 backdrop-blur-sm">
          {submitted ? (
            <div className="text-center py-12 space-y-4">
              <div className="w-16 h-16 bg-emerald-600/20 text-emerald-400 rounded-full flex items-center justify-center mx-auto text-2xl font-bold">
                ✓
              </div>
              <h2 className="text-2xl font-bold text-white">Votre message a été transmis !</h2>
              <p className="text-slate-300 text-sm max-w-md mx-auto">
                Merci de votre confiance. Notre collège pastoral prendra le temps de prier pour votre situation et vous recontactera avec bienveillance.
              </p>
              <button
                onClick={() => setSubmitted(false)}
                className="mt-4 px-6 py-2 bg-blue-600 text-white rounded-xl text-sm font-semibold hover:bg-blue-500 transition-colors"
              >
                Envoyer un autre message
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-6">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Nom complet *</label>
                  <input
                    type="text"
                    required
                    value={formData.name}
                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-blue-500"
                    placeholder="Jean-Paul Mukendi"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Téléphone / WhatsApp</label>
                  <input
                    type="tel"
                    value={formData.phone}
                    onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                    className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-blue-500"
                    placeholder="+243 ..."
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1">Adresse Email *</label>
                <input
                  type="email"
                  required
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-blue-500"
                  placeholder="contact@exemple.org"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1">Objet de votre demande *</label>
                <select
                  value={formData.type}
                  onChange={(e) => setFormData({ ...formData, type: e.target.value })}
                  className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-blue-500"
                >
                  <option value="priere">Sujet / Requête de prière confidentielle</option>
                  <option value="ecoute">Rendez-vous / Entretien pastoral</option>
                  <option value="temoignage">Partager un témoignage de bénédiction</option>
                  <option value="information">Information générale sur les paroisses</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1">Votre message / sujet de prière *</label>
                <textarea
                  required
                  rows={5}
                  value={formData.message}
                  onChange={(e) => setFormData({ ...formData, message: e.target.value })}
                  className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-blue-500"
                  placeholder="Expliquez votre situation en toute liberté..."
                />
              </div>

              <button
                type="submit"
                className="w-full py-3 bg-blue-600 hover:bg-blue-500 text-white font-bold rounded-xl text-sm transition-colors shadow-lg shadow-blue-600/30"
              >
                Transmettre ma requête à l'équipe pastorale
              </button>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
