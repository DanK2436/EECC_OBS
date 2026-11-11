import React from "react";
import Link from "next/link";
import { Radio, Calendar, Play, Clock, MapPin } from "lucide-react";

interface LiveCardProps {
  isLive?: boolean;
}

export const LiveCard: React.FC<LiveCardProps> = ({ isLive = false }) => {
  return (
    <section className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 -mt-12 relative z-10">
      <div className="bg-white rounded-2xl shadow-xl border border-slate-200/80 overflow-hidden">
        {isLive ? (
          // État 1 : Direct en cours
          <div className="p-6 sm:p-8 bg-gradient-to-r from-red-50 to-white flex flex-col md:flex-row items-center justify-between gap-6">
            <div className="flex items-start space-x-4">
              <div className="w-12 h-12 rounded-xl bg-red-600 text-white flex items-center justify-center flex-shrink-0 animate-pulse">
                <Radio className="w-6 h-6" />
              </div>
              <div>
                <div className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold bg-red-600 text-white mb-2">
                  <span className="w-2 h-2 rounded-full bg-white mr-1.5 animate-ping" />
                  EN DIRECT MAINTENANT
                </div>
                <h2 className="text-xl sm:text-2xl font-bold text-textMain">Culte de Célébration & Enseignement</h2>
                <p className="text-sm text-slate mt-1">Rejoignez-nous en direct pour écouter la Parole et louer ensemble.</p>
              </div>
            </div>
            <Link
              href="/cultes"
              className="inline-flex items-center px-6 py-3 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 transition-colors shadow-md shadow-red-600/20"
            >
              <Play className="w-5 h-5 mr-2 fill-current" />
              Rejoindre le direct
            </Link>
          </div>
        ) : (
          // État 2 : Aucun direct en cours (Prochain rendez-vous)
          <div className="p-6 sm:p-8 flex flex-col md:flex-row items-center justify-between gap-6">
            <div className="flex items-start space-x-4">
              <div className="w-12 h-12 rounded-xl bg-primary/10 text-primary flex items-center justify-center flex-shrink-0">
                <Calendar className="w-6 h-6" />
              </div>
              <div>
                <span className="text-xs font-bold text-primary uppercase tracking-wider block mb-1">
                  Prochaine Célébration
                </span>
                <h2 className="text-xl sm:text-2xl font-bold text-textMain">Grand Culte Dominical</h2>
                <div className="flex flex-wrap items-center gap-4 mt-2 text-sm text-slate">
                  <span className="flex items-center">
                    <Clock className="w-4 h-4 mr-1 text-primary" /> Dimanche à 08h30 & 11h00
                  </span>
                  <span className="flex items-center">
                    <MapPin className="w-4 h-4 mr-1 text-primary" /> Siège Principal, Matadi
                  </span>
                </div>
              </div>
            </div>

            <div className="flex items-center space-x-3 w-full md:w-auto">
              <Link
                href="/cultes"
                className="w-full md:w-auto inline-flex items-center justify-center px-5 py-3 rounded-xl font-bold text-primary bg-primary/10 hover:bg-primary/15 transition-colors text-sm"
              >
                Voir les replays récents
              </Link>
              <Link
                href="/nos-eglises"
                className="w-full md:w-auto inline-flex items-center justify-center px-5 py-3 rounded-xl font-bold text-white bg-primary hover:bg-primary-dark transition-colors text-sm"
              >
                Itinéraire
              </Link>
            </div>
          </div>
        )}
      </div>
    </section>
  );
};
