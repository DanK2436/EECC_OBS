import React from "react";
import Link from "next/link";
import { Play, ArrowRight, Heart } from "lucide-react";

export const HeroBanner = () => {
  return (
    <section className="relative overflow-hidden bg-gradient-to-br from-slate-900 via-primary-dark to-slate-900 text-white py-24 sm:py-32">
      {/* Texture subtile en arrière-plan */}
      <div className="absolute inset-0 opacity-10 bg-[radial-gradient(#3B82F6_1px,transparent_1px)] [background-size:24px_24px]" />

      <div className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
        <div className="inline-flex items-center px-4 py-1.5 rounded-full bg-white/10 backdrop-blur-md text-primary-light text-xs font-bold uppercase tracking-wider mb-6 border border-white/10">
          Communauté de foi • Célébration • Impact
        </div>

        <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold tracking-tight max-w-4xl mx-auto text-balance">
          Bienvenue à l’<span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-400 to-indigo-300">EECC</span>
        </h1>

        <p className="mt-6 text-lg sm:text-xl text-slate-300 max-w-2xl mx-auto leading-relaxed">
          Une communauté vivante, ancrée dans la Parole de Dieu et unie pour transformer des vies par l’amour du Christ.
        </p>

        <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link
            href="/cultes"
            className="w-full sm:w-auto inline-flex items-center justify-center px-7 py-3.5 rounded-xl font-bold text-white bg-primary hover:bg-primary-light transition-all shadow-lg shadow-primary/30"
          >
            <Play className="w-5 h-5 mr-2 fill-current" />
            Regarder le direct
          </Link>
          <Link
            href="/a-propos"
            className="w-full sm:w-auto inline-flex items-center justify-center px-7 py-3.5 rounded-xl font-bold text-slate-200 bg-white/10 hover:bg-white/15 backdrop-blur-md transition-colors border border-white/10"
          >
            Découvrir l’église
            <ArrowRight className="w-4 h-4 ml-2" />
          </Link>
          <Link
            href="/don"
            className="w-full sm:w-auto inline-flex items-center justify-center px-6 py-3.5 rounded-xl font-bold text-pink-300 bg-pink-500/10 hover:bg-pink-500/20 backdrop-blur-md transition-colors border border-pink-500/20"
          >
            <Heart className="w-4 h-4 mr-2 fill-current" />
            Soutenir la mission
          </Link>
        </div>
      </div>
    </section>
  );
};
