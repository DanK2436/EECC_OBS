import React from "react";
import Link from "next/link";
import { Heart, ShieldCheck, Smartphone, CreditCard } from "lucide-react";

export const DonationCTA = () => {
  return (
    <section className="py-20 bg-gradient-to-br from-primary-dark via-primary to-slate-900 text-white relative overflow-hidden">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
          <div>
            <div className="inline-flex items-center px-3 py-1 rounded-full bg-white/10 text-pink-300 text-xs font-bold uppercase tracking-wider mb-4 border border-white/10">
              <Heart className="w-3.5 h-3.5 mr-1.5 fill-current" />
              Soutenir l’Évangile
            </div>
            <h2 className="text-3xl sm:text-4xl font-extrabold tracking-tight mb-4">
              Participez à la mission et à l’avancement de l’œuvre
            </h2>
            <p className="text-slate-300 text-base leading-relaxed mb-8">
              Chaque don, dîme et offrande permet de soutenir nos cultes, nos actions sociales, la diffusion numérique et la construction de nos lieux de prière.
            </p>

            <div className="grid grid-cols-2 gap-4 mb-8">
              <div className="bg-white/10 backdrop-blur-md rounded-xl p-4 border border-white/10">
                <Smartphone className="w-6 h-6 text-pink-300 mb-2" />
                <h4 className="font-bold text-sm">Mobile Money</h4>
                <p className="text-xs text-slate-300">Airtel Money, M-Pesa, Orange Money en RDC.</p>
              </div>
              <div className="bg-white/10 backdrop-blur-md rounded-xl p-4 border border-white/10">
                <CreditCard className="w-6 h-6 text-blue-300 mb-2" />
                <h4 className="font-bold text-sm">Carte Bancaire</h4>
                <p className="text-xs text-slate-300">Visa et Mastercard internationales.</p>
              </div>
            </div>

            <div className="flex items-center space-x-2 text-xs text-slate-400">
              <ShieldCheck className="w-4 h-4 text-green-400" />
              <span>Transactions cryptées et sécurisées par la passerelle agréée Maishapay.</span>
            </div>
          </div>

          <div className="bg-white text-textMain rounded-2xl p-8 shadow-2xl border border-white/20">
            <h3 className="text-2xl font-bold mb-2">Faire un don en ligne</h3>
            <p className="text-sm text-slate mb-6">
              Choisissez votre montant ou participez selon votre cœur.
            </p>

            <div className="grid grid-cols-3 gap-3 mb-6">
              {["10 $", "25 $", "50 $", "100 $", "250 $", "Libre"].map((amt, i) => (
                <button
                  key={i}
                  className="py-3 px-2 text-center rounded-xl font-bold text-sm border border-slate-200 hover:border-primary hover:bg-primary/5 transition-all"
                >
                  {amt}
                </button>
              ))}
            </div>

            <Link
              href="/don"
              className="w-full flex items-center justify-center py-4 rounded-xl font-bold text-white bg-primary hover:bg-primary-dark transition-colors shadow-lg shadow-primary/20 text-base"
            >
              <Heart className="w-5 h-5 mr-2 fill-current" />
              Continuer vers le don sécurisé
            </Link>

            <p className="text-center text-xs text-slate-400 mt-4">
              Reçu de don instantané téléchargeable après confirmation.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
};
