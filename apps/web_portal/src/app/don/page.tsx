"use client";

import React, { useState } from "react";
import { Navbar } from "@/components/layout/Navbar";
import { Footer } from "@/components/layout/Footer";
import { Heart, ShieldCheck, Smartphone, CreditCard, CheckCircle2 } from "lucide-react";

export default function DonationPage() {
  const [selectedAmount, setSelectedAmount] = useState<number>(20);
  const [customAmount, setCustomAmount] = useState<string>("");
  const [paymentMethod, setPaymentMethod] = useState<string>("mobile_money");
  const [donorName, setDonorName] = useState<string>("");
  const [donorPhone, setDonorPhone] = useState<string>("");
  const [donorEmail, setDonorEmail] = useState<string>("");
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);

  const predefinedAmounts = [5, 10, 20, 50, 100, 200];

  const handleDonate = (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    // Simulation d'appel sécurisé vers l'Edge function Supabase Maishapay
    setTimeout(() => {
      setIsSubmitting(false);
      setIsSuccess(true);
    }, 1200);
  };

  const finalAmount = customAmount ? parseFloat(customAmount) || 0 : selectedAmount;

  return (
    <>
      <Navbar />
      <main className="flex-grow py-14 bg-background">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-12">
            <div className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold text-pink-600 bg-pink-50 mb-3 border border-pink-100">
              <Heart className="w-3.5 h-3.5 mr-1.5 fill-current" />
              Générosité & Action de Grâce
            </div>
            <h1 className="text-4xl font-extrabold text-textMain tracking-tight">
              Soutenir l’Église EECC
            </h1>
            <p className="mt-3 text-base text-slate max-w-xl mx-auto">
              « Que chacun donne comme il l’a résolu en son cœur, sans tristesse ni contrainte ; car Dieu aime celui qui donne avec joie. » (2 Corinthiens 9:7)
            </p>
          </div>

          <div className="bg-white rounded-3xl border border-slate-200/80 shadow-xl overflow-hidden p-8 sm:p-12">
            {isSuccess ? (
              <div className="text-center py-12">
                <div className="w-16 h-16 rounded-full bg-green-100 text-green-600 flex items-center justify-center mx-auto mb-4">
                  <CheckCircle2 className="w-10 h-10" />
                </div>
                <h2 className="text-2xl font-bold text-textMain mb-2">Merci pour votre générosité !</h2>
                <p className="text-sm text-slate max-w-md mx-auto mb-6">
                  Votre intention de don de <strong>{finalAmount} $</strong> a été enregistrée avec succès. Vous recevrez la confirmation et le reçu après validation de votre opérateur.
                </p>
                <button
                  onClick={() => setIsSuccess(false)}
                  className="px-6 py-2.5 rounded-xl text-sm font-bold text-white bg-primary hover:bg-primary-dark transition-colors"
                >
                  Effectuer un autre don
                </button>
              </div>
            ) : (
              <form onSubmit={handleDonate} className="space-y-8">
                {/* 1. Choix du montant */}
                <div>
                  <label className="block text-sm font-bold text-textMain mb-3">
                    1. Choisissez votre montant (USD) :
                  </label>
                  <div className="grid grid-cols-3 sm:grid-cols-6 gap-3 mb-4">
                    {predefinedAmounts.map((amt) => {
                      const isSelected = selectedAmount === amt && !customAmount;
                      return (
                        <button
                          key={amt}
                          type="button"
                          onClick={() => {
                            setSelectedAmount(amt);
                            setCustomAmount("");
                          }}
                          className={`py-3 rounded-xl font-bold text-sm border transition-all ${
                            isSelected
                              ? "bg-primary text-white border-primary shadow-md shadow-primary/20"
                              : "bg-white text-textMain border-slate-200 hover:border-primary/40"
                          }`}
                        >
                          {amt} $
                        </button>
                      );
                    })}
                  </div>

                  <input
                    type="number"
                    placeholder="Ou saisissez un autre montant personnalisé ($)"
                    value={customAmount}
                    onChange={(e) => setCustomAmount(e.target.value)}
                    className="w-full px-4 py-3 text-sm bg-background rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  />
                </div>

                {/* 2. Mode de paiement */}
                <div>
                  <label className="block text-sm font-bold text-textMain mb-3">
                    2. Sélectionnez le mode de paiement :
                  </label>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <button
                      type="button"
                      onClick={() => setPaymentMethod("mobile_money")}
                      className={`p-4 rounded-2xl border text-left flex items-start space-x-3 transition-all ${
                        paymentMethod === "mobile_money"
                          ? "border-primary bg-primary/5 ring-2 ring-primary/20"
                          : "border-slate-200 bg-white"
                      }`}
                    >
                      <Smartphone className="w-6 h-6 text-primary flex-shrink-0 mt-0.5" />
                      <div>
                        <strong className="block text-sm font-bold text-textMain">Mobile Money (RDC)</strong>
                        <span className="text-xs text-slate">Airtel Money, M-Pesa, Orange Money</span>
                      </div>
                    </button>

                    <button
                      type="button"
                      onClick={() => setPaymentMethod("card")}
                      className={`p-4 rounded-2xl border text-left flex items-start space-x-3 transition-all ${
                        paymentMethod === "card"
                          ? "border-primary bg-primary/5 ring-2 ring-primary/20"
                          : "border-slate-200 bg-white"
                      }`}
                    >
                      <CreditCard className="w-6 h-6 text-primary flex-shrink-0 mt-0.5" />
                      <div>
                        <strong className="block text-sm font-bold text-textMain">Carte Bancaire</strong>
                        <span className="text-xs text-slate">Visa, Mastercard internationale</span>
                      </div>
                    </button>
                  </div>
                </div>

                {/* 3. Coordonnées */}
                <div className="space-y-4">
                  <label className="block text-sm font-bold text-textMain">
                    3. Vos coordonnées pour le reçu :
                  </label>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <input
                        type="text"
                        required
                        placeholder="Nom complet ou Anonyme"
                        value={donorName}
                        onChange={(e) => setDonorName(e.target.value)}
                        className="w-full px-4 py-3 text-sm bg-background rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                      />
                    </div>
                    <div>
                      <input
                        type="tel"
                        required
                        placeholder="Numéro de téléphone (+243...)"
                        value={donorPhone}
                        onChange={(e) => setDonorPhone(e.target.value)}
                        className="w-full px-4 py-3 text-sm bg-background rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                      />
                    </div>
                  </div>
                  <div>
                    <input
                      type="email"
                      placeholder="Adresse e-mail (facultative pour le reçu électronique)"
                      value={donorEmail}
                      onChange={(e) => setDonorEmail(e.target.value)}
                      className="w-full px-4 py-3 text-sm bg-background rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                    />
                  </div>
                </div>

                {/* Bouton de confirmation */}
                <div className="pt-4 border-t border-slate-100">
                  <button
                    type="submit"
                    disabled={isSubmitting || finalAmount <= 0}
                    className="w-full py-4 rounded-xl font-bold text-white bg-primary hover:bg-primary-dark transition-all shadow-lg shadow-primary/25 disabled:opacity-50 text-base flex items-center justify-center"
                  >
                    <Heart className="w-5 h-5 mr-2 fill-current" />
                    {isSubmitting
                      ? "Traitement en cours..."
                      : `Confirmer mon don de ${finalAmount} $`}
                  </button>
                  <div className="flex items-center justify-center space-x-2 text-xs text-slate-400 mt-4">
                    <ShieldCheck className="w-4 h-4 text-green-500" />
                    <span>Paiement chiffré et sécurisé par Maishapay. Aucun numéro de carte n'est conservé.</span>
                  </div>
                </div>
              </form>
            )}
          </div>
        </div>
      </main>
      <Footer />
    </>
  );
}
