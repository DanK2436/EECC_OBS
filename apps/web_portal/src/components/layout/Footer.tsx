import React from "react";
import Link from "next/link";
import { Church, MapPin, Phone, Mail, Clock, Heart } from "lucide-react";

export const Footer = () => {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="bg-slate-900 text-white border-t border-slate-800">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-16 pb-12">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-10">
          {/* Colonne 1 : Présentation */}
          <div>
            <div className="flex items-center space-x-3 mb-5">
              <div className="w-10 h-10 rounded-xl overflow-hidden bg-white flex items-center justify-center p-0.5 shadow-md">
                <img src="/logo.png" alt="Logo EECC" className="w-full h-full object-contain" />
              </div>
              <span className="text-xl font-bold tracking-tight">EECC</span>
            </div>
            <p className="text-slate-400 text-sm leading-relaxed mb-6">
              Église Évangélique du Congo Central. Une communauté vivante dédiée à l’adoration, l’édification et le salut par Jésus-Christ.
            </p>
            <Link
              href="/don"
              className="inline-flex items-center px-4 py-2 rounded-lg text-xs font-bold text-white bg-primary hover:bg-primary-light transition-colors"
            >
              <Heart className="w-3.5 h-3.5 mr-1.5 fill-current" />
              Soutenir l’église
            </Link>
          </div>

          {/* Colonne 2 : Liens Rapides */}
          <div>
            <h3 className="text-sm font-bold uppercase tracking-wider text-slate-200 mb-4">Navigation</h3>
            <ul className="space-y-2.5 text-sm text-slate-400">
              <li><Link href="/" className="hover:text-white transition-colors">Accueil</Link></li>
              <li><Link href="/a-propos" className="hover:text-white transition-colors">Notre vision & Histoire</Link></li>
              <li><Link href="/nos-eglises" className="hover:text-white transition-colors">Nos implantations</Link></li>
              <li><Link href="/cultes" className="hover:text-white transition-colors">Cultes en direct & Replays</Link></li>
              <li><Link href="/mediatheque" className="hover:text-white transition-colors">Podcasts & Prédications</Link></li>
              <li><Link href="/admin" className="hover:text-white transition-colors text-xs text-slate-500">Console Administration</Link></li>
            </ul>
          </div>

          {/* Colonne 3 : Horaires des Cultes */}
          <div>
            <h3 className="text-sm font-bold uppercase tracking-wider text-slate-200 mb-4">Nos Horaires</h3>
            <ul className="space-y-3 text-sm text-slate-400">
              <li className="flex items-start">
                <Clock className="w-4 h-4 mr-2 text-primary-light flex-shrink-0 mt-0.5" />
                <div>
                  <strong className="text-white block">Dimanche Matin</strong>
                  08h30 — 1er Culte<br />
                  11h00 — 2ème Culte
                </div>
              </li>
              <li className="flex items-start">
                <Clock className="w-4 h-4 mr-2 text-primary-light flex-shrink-0 mt-0.5" />
                <div>
                  <strong className="text-white block">Mercredi Soir</strong>
                  17h30 — Étude Biblique & Prière
                </div>
              </li>
            </ul>
          </div>

          {/* Colonne 4 : Coordonnées Officielles */}
          <div>
            <h3 className="text-sm font-bold uppercase tracking-wider text-slate-200 mb-4">Contact</h3>
            <ul className="space-y-3 text-sm text-slate-400">
              <li className="flex items-start">
                <MapPin className="w-4 h-4 mr-2.5 text-primary-light flex-shrink-0 mt-0.5" />
                <span>Avenue de la Paix, Matadi, République Démocratique du Congo</span>
              </li>
              <li className="flex items-center">
                <Phone className="w-4 h-4 mr-2.5 text-primary-light flex-shrink-0" />
                <span>+243 81 000 0000</span>
              </li>
              <li className="flex items-center">
                <Mail className="w-4 h-4 mr-2.5 text-primary-light flex-shrink-0" />
                <span>contact@eecc-eglise.org</span>
              </li>
            </ul>
          </div>
        </div>

        <div className="mt-14 pt-8 border-t border-slate-800 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-500 space-y-4 sm:space-y-0">
          <p>© {currentYear} EECC — Église Évangélique. Tous droits réservés.</p>
          <div className="flex space-x-6">
            <Link href="/mentions-legales" className="hover:text-slate-400">Mentions légales</Link>
            <Link href="/confidentialite" className="hover:text-slate-400">Confidentialité</Link>
            <Link href="/don" className="hover:text-slate-400">Dons & Transparence</Link>
          </div>
        </div>
      </div>
    </footer>
  );
};
