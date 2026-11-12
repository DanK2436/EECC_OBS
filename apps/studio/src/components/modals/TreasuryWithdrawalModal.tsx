import React, { useState } from "react";
import {
  X,
  DollarSign,
  ShieldAlert,
  ShieldCheck,
  CheckCircle2,
  AlertTriangle,
  RefreshCw,
  Mail,
  ArrowRight,
} from "lucide-react";

interface TreasuryWithdrawalModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const TreasuryWithdrawalModal: React.FC<TreasuryWithdrawalModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [step, setStep] = useState<"form" | "otp" | "success">("form");
  const [amount, setAmount] = useState<number>(500);
  const [destinationProvider, setDestinationProvider] = useState<string>("M-Pesa (Vodacom)");
  const [destinationAccount, setDestinationAccount] = useState<string>("+243 81 234 5678");
  const [securityRecipientEmail, setSecurityRecipientEmail] = useState<string>("");
  const [isEditingEmail, setIsEditingEmail] = useState<boolean>(false);
  const [otpCode, setOtpCode] = useState<string>("");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [remainingAttempts, setRemainingAttempts] = useState<number>(5);

  if (!isOpen) return null;

  const knownFee = (amount * 0.015).toFixed(2);
  const netAmount = (amount - parseFloat(knownFee)).toFixed(2);

  // Étape 1 : Demande d'OTP via Brevo
  const handleRequestOtp = () => {
    if (!securityRecipientEmail || !securityRecipientEmail.includes("@")) {
      setErrorMessage("Veuillez renseigner une adresse e-mail de réception valide pour l'OTP.");
      setIsEditingEmail(true);
      return;
    }

    setIsLoading(true);
    setErrorMessage(null);

    setIsLoading(false);
    setErrorMessage("Aucun code n'a été envoyé. L'envoi Brevo et le décaissement MaishaPay ne sont pas branchés.");
  };

  // Étape 2 : Vérification du code 5 caractères via 'withdrawal-verify-otp'
  const handleVerifyOtp = (e: React.FormEvent) => {
    e.preventDefault();
    if (otpCode.trim().length !== 5) {
      setErrorMessage("Le code de vérification doit comporter exactement 5 caractères.");
      return;
    }

    setIsLoading(true);
    setErrorMessage(null);

    setIsLoading(false);
    setErrorMessage("Ce code n'est pas vérifié. Aucun retrait n'est envoyé à MaishaPay.");
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 select-none">
      <div className="bg-white rounded-2xl w-full max-w-lg shadow-2xl border border-slate-200 overflow-hidden">
        {/* En-tête */}
        <div className="px-6 py-4 bg-slate-900 text-white flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <DollarSign className="w-5 h-5 text-emerald-400" />
            <div>
              <h3 className="font-bold text-sm">Trésorerie & Retrait Sécurisé</h3>
              <span className="text-[10px] text-slate-400">Passerelle Maishapay • Conformité Section 20</span>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Corps */}
        <div className="p-6">
          {step === "form" && (
            <div className="space-y-5">
              {/* Solde actuel */}
              <div className="bg-emerald-50 rounded-xl p-4 border border-emerald-200 flex items-center justify-between">
                <div>
                  <span className="text-xs text-emerald-800 font-semibold block">Solde Disponible Maishapay</span>
                  <span className="text-2xl font-extrabold text-emerald-900">4 850,00 $</span>
                </div>
                <span className="text-[10px] bg-emerald-200 text-emerald-900 px-2 py-0.5 rounded font-bold">
                  Compte vérifié
                </span>
              </div>

              {/* Formulaire de Retrait */}
              <div className="space-y-4">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Montant brut à retirer (USD) :
                  </label>
                  <input
                    type="number"
                    value={amount}
                    onChange={(e) => setAmount(Number(e.target.value))}
                    className="w-full px-3 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary font-mono font-bold"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Opérateur de retrait :
                  </label>
                  <select
                    value={destinationProvider}
                    onChange={(e) => setDestinationProvider(e.target.value)}
                    className="w-full px-3 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
                  >
                    <option value="M-Pesa (Vodacom)">M-Pesa (Vodacom)</option>
                    <option value="Airtel Money">Airtel Money</option>
                    <option value="Orange Money">Orange Money</option>
                    <option value="Virement Bancaire (RDC)">Virement Bancaire (RDC)</option>
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Compte ou Téléphone bénéficiaire :
                  </label>
                  <input
                    type="text"
                    value={destinationAccount}
                    onChange={(e) => setDestinationAccount(e.target.value)}
                    className="w-full px-3 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary font-mono"
                  />
                </div>

                {/* Configuration manuelle de l'email de réception OTP */}
                <div className="pt-2 border-t border-slate-100">
                  <div className="flex items-center justify-between mb-1">
                    <label className="block text-xs font-bold text-slate-700">
                      Adresse e-mail de réception du code OTP :
                    </label>
                    <span className="text-[10px] text-slate-400">
                      (Expéditeur Brevo : kandedan28@gmail.com)
                    </span>
                  </div>
                  <input
                    type="email"
                    required
                    placeholder="ex: tresorier-eglise@gmail.com"
                    value={securityRecipientEmail}
                    onChange={(e) => setSecurityRecipientEmail(e.target.value)}
                    className="w-full px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                  <span className="text-[10px] text-slate-500 mt-1 block">
                    Saisie manuelle : Le code à 5 caractères sera envoyé à cette adresse de sécurité.
                  </span>
                </div>
              </div>

              {/* Récapitulatif Transparence */}
              <div className="bg-slate-50 rounded-xl p-3 border border-slate-200 space-y-1.5 text-xs">
                <div className="flex justify-between text-slate-500">
                  <span>Montant brut :</span>
                  <span className="font-mono font-bold text-slate-700">{amount.toFixed(2)} $</span>
                </div>
                <div className="flex justify-between text-slate-500">
                  <span>Frais estimés (1.5%) :</span>
                  <span className="font-mono text-slate-700">{knownFee} $</span>
                </div>
                <div className="flex justify-between pt-1 border-t border-slate-200 font-bold text-slate-900">
                  <span>Montant net affiché, non versé :</span>
                  <span className="font-mono text-emerald-600">{netAmount} $</span>
                </div>
              </div>

              {/* Alerte Sécurité OTP */}
              <div className="flex items-start space-x-2 text-[11px] text-amber-800 bg-amber-50 p-3 rounded-xl border border-amber-200">
                <ShieldAlert className="w-4 h-4 text-amber-600 flex-shrink-0 mt-0.5" />
                <span>
                  Aucun code n'est envoyé et aucun décaissement n'est lancé. Brevo et MaishaPay ne sont pas branchés.
                </span>
              </div>
              {errorMessage && step === "form" && (
                <div className="p-2.5 rounded-lg bg-red-50 text-red-700 text-xs text-center border border-red-200 font-semibold">
                  {errorMessage}
                </div>
              )}

              <button
                onClick={handleRequestOtp}
                disabled={isLoading || amount <= 0}
                className="w-full py-3 bg-primary hover:bg-primary-dark text-white rounded-xl text-xs font-bold transition-all shadow-md flex items-center justify-center space-x-2"
              >
                {isLoading ? (
                  <span>Génération de l'OTP en cours...</span>
                ) : (
                  <>
                    <span>Continuer vers la vérification OTP</span>
                    <ArrowRight className="w-4 h-4" />
                  </>
                )}
              </button>
            </div>
          )}

          {step === "otp" && (
            <form onSubmit={handleVerifyOtp} className="space-y-5">
              <div className="text-center">
                <div className="w-12 h-12 bg-primary/10 text-primary rounded-full flex items-center justify-center mx-auto mb-3">
                  <Mail className="w-6 h-6" />
                </div>
                <h4 className="text-base font-bold text-textMain">Confirmez votre retrait</h4>
                <p className="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
                  Aucun code n'a été envoyé. Saisir un code ici ne déclenche pas de retrait.
                </p>
              </div>

              {/* Champ Code 5 Caractères */}
              <div className="flex justify-center my-4">
                <input
                  type="text"
                  maxLength={5}
                  value={otpCode}
                  onChange={(e) => setOtpCode(e.target.value.toUpperCase())}
                  placeholder="Code"
                  autoFocus
                  className="w-48 py-3 text-center text-2xl font-mono font-extrabold tracking-widest bg-slate-50 border-2 border-primary rounded-xl focus:outline-none uppercase shadow-inner"
                />
              </div>

              {errorMessage && (
                <div className="p-2.5 rounded-lg bg-red-50 text-red-700 text-xs text-center border border-red-200 font-semibold">
                  {errorMessage}
                </div>
              )}

              <div className="text-center text-[11px] text-slate-400">
                Code valide pendant 5 minutes • Essais restants : {remainingAttempts}
              </div>

              <div className="space-y-2 pt-2">
                <button
                  type="submit"
                  disabled={isLoading || otpCode.length !== 5}
                  className="w-full py-3 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-bold transition-all shadow-md flex items-center justify-center space-x-2 disabled:opacity-50"
                >
                  {isLoading ? (
                    <span>Vérification & Décaissement Maishapay...</span>
                  ) : (
                    <span>Vérifier et confirmer le retrait</span>
                  )}
                </button>

                <button
                  type="button"
                  onClick={() => setStep("form")}
                  className="w-full py-2 text-slate-500 hover:text-slate-800 text-xs font-semibold"
                >
                  Annuler le retrait
                </button>
              </div>
            </form>
          )}

          {step === "success" && (
            <div className="text-center py-6 space-y-4">
              <div className="w-16 h-16 bg-green-100 text-green-600 rounded-full flex items-center justify-center mx-auto">
                <CheckCircle2 className="w-10 h-10" />
              </div>
              <h4 className="text-xl font-bold text-textMain">Retrait non envoyé</h4>
              <p className="text-xs text-slate-600 max-w-sm mx-auto">
                Aucun fonds n'a été envoyé. MaishaPay n'a pas confirmé de décaissement.
              </p>
              <div className="p-3 bg-slate-50 rounded-xl text-[11px] text-slate-500">
                Aucune référence prestataire n'a été reçue.
              </div>
              <button
                onClick={() => {
                  setStep("form");
                  onClose();
                }}
                className="px-6 py-2.5 bg-primary text-white rounded-xl text-xs font-bold"
              >
                Fermer
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
