import React from "react";
import Link from "next/link";
import { Clock, MapPin, ArrowRight } from "lucide-react";

export const SchedulesSection = () => {
  const schedules = [
    {
      day: "Dimanche Matin",
      time: "08:30 — 10:30",
      title: "Premier Culte de Célébration",
      desc: "Louange vivante, prédication inspirante et Sainte-Cène.",
      location: "Grand Temple, Matadi",
    },
    {
      day: "Dimanche Matin",
      time: "11:00 — 13:00",
      title: "Deuxième Culte de Célébration",
      desc: "Même ferveur pour les familles et la jeunesse.",
      location: "Grand Temple, Matadi",
    },
    {
      day: "Mercredi Soir",
      time: "17:30 — 19:30",
      title: "Étude Biblique & Intercession",
      desc: "Approfondissement doctrinal des Écritures et prière fervente.",
      location: "Salle Polyvalente, Matadi",
    },
  ];

  return (
    <section className="py-20 bg-background">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-3xl mx-auto mb-14">
          <span className="text-xs font-bold text-primary uppercase tracking-wider block mb-2">
            Rendez-vous hebdomadaires
          </span>
          <h2 className="text-3xl font-extrabold text-textMain tracking-tight">Nos Horaires de Culte</h2>
          <p className="mt-3 text-base text-slate">
            Venez nous rejoindre en présentiel ou connectez-vous via EECC TV. Vous êtes les bienvenus !
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          {schedules.map((item, idx) => (
            <div
              key={idx}
              className="bg-white rounded-2xl p-7 border border-slate-200/80 shadow-sm hover:shadow-md transition-shadow flex flex-col justify-between"
            >
              <div>
                <div className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold text-primary bg-primary/10 mb-4">
                  <Clock className="w-3.5 h-3.5 mr-1.5" />
                  {item.day} • {item.time}
                </div>
                <h3 className="text-xl font-bold text-textMain mb-2">{item.title}</h3>
                <p className="text-sm text-slate leading-relaxed mb-6">{item.desc}</p>
              </div>

              <div className="pt-4 border-t border-slate-100 flex items-center justify-between text-xs text-slate">
                <span className="flex items-center font-medium">
                  <MapPin className="w-3.5 h-3.5 mr-1 text-primary" /> {item.location}
                </span>
                <Link href="/nos-eglises" className="font-bold text-primary hover:underline flex items-center">
                  Plan <ArrowRight className="w-3 h-3 ml-1" />
                </Link>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
};
