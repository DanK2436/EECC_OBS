import React from 'react';
import { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Nos Églises & Paroisses — EECC',
  description: 'Trouvez l\'église EECC la plus proche de chez vous : adresses, pasteurs et horaires des cultes.',
};

interface ChurchLocation {
  id: string;
  name: string;
  city: string;
  address: string;
  pastor: string;
  phone: string;
  services: string[];
}

const locations: ChurchLocation[] = [
  {
    id: 'siege-central',
    name: 'EECC Siège Central — Temple de la Victoire',
    city: 'Kinshasa',
    address: 'Av. de la Paix, Q. Résidentiel, RDC',
    pastor: 'Pasteur Responsable Principal',
    phone: '+243 81 000 0001',
    services: [
      'Dimanche : Culte d\'Adoration (08h00 - 11h30)',
      'Mercredi : Enseignement & Prière (17h00 - 19h00)',
      'Vendredi : Veillée de Délivrance (21h00 - 04h00)',
    ],
  },
  {
    id: 'paroisse-est',
    name: 'EECC Paroisse Grâce & Rédemption',
    city: 'Lubumbashi',
    address: 'Boulevard Katanga, Ville Haute',
    pastor: 'Pasteur Associé',
    phone: '+243 82 000 0002',
    services: [
      'Dimanche : Culte Dominical (08h30 - 11h00)',
      'Jeudi : École du Ministère (17h30 - 19h00)',
    ],
  },
  {
    id: 'paroisse-nord',
    name: 'EECC Paroisse Nouvelle Espérance',
    city: 'Goma',
    address: 'Avenue du Lac Kivu, Q. Himbi',
    pastor: 'Pasteur Régional',
    phone: '+243 85 000 0003',
    services: [
      'Dimanche : Culte d\'Actions de Grâce (09h00 - 11h30)',
      'Mardi : Prière d\'Intercession (16h30 - 18h30)',
    ],
  },
];

export default function EglisesPage() {
  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 py-16 px-4 sm:px-6 lg:px-8">
      <div className="max-w-5xl mx-auto space-y-12">
        <div className="text-center space-y-3">
          <span className="inline-block px-3 py-1 bg-emerald-600/20 text-emerald-400 rounded-full text-xs font-semibold uppercase tracking-wider">
            Implantations &amp; Communautés
          </span>
          <h1 className="text-4xl sm:text-5xl font-black text-white tracking-tight">
            Nos Églises &amp; Paroisses
          </h1>
          <p className="text-slate-300 max-w-2xl mx-auto">
            Trouvez une église locale EECC pour adorer Dieu, grandir dans la foi et vivre la communion fraternelle.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {locations.map((loc) => (
            <div
              key={loc.id}
              className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-6 flex flex-col justify-between space-y-6 hover:border-blue-500/50 transition-colors"
            >
              <div className="space-y-4">
                <div className="inline-block px-2.5 py-0.5 bg-blue-600/20 text-blue-400 rounded text-xs font-medium">
                  {loc.city}
                </div>
                <h2 className="text-xl font-bold text-white leading-snug">{loc.name}</h2>
                <div className="text-xs text-slate-400 space-y-1">
                  <p>📍 {loc.address}</p>
                  <p>👤 {loc.pastor}</p>
                  <p>📞 {loc.phone}</p>
                </div>
                <div className="border-t border-slate-700/60 pt-3">
                  <h3 className="text-xs font-bold text-slate-300 uppercase tracking-wider mb-2">
                    Horaires des réunions :
                  </h3>
                  <ul className="space-y-1 text-xs text-slate-400">
                    {loc.services.map((srv, idx) => (
                      <li key={idx} className="flex items-start gap-1.5">
                        <span className="text-emerald-400 font-bold">•</span>
                        <span>{srv}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>

              <a
                href={`tel:${loc.phone}`}
                className="w-full text-center py-2 px-4 bg-slate-700/80 hover:bg-blue-600 text-white rounded-xl text-xs font-semibold transition-colors"
              >
                Contacter la paroisse
              </a>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
