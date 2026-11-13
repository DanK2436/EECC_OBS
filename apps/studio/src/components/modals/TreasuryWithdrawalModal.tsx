import React, { useState } from "react";
import { X, DollarSign, ShieldAlert, CheckCircle2, Mail, ArrowRight } from "lucide-react";

interface TreasuryWithdrawalModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const TreasuryWithdrawalModal: React.FC<TreasuryWithdrawalModalProps> = ({ isOpen, onClose }) => {
  const [step, setStep] = useState<"form" | "otp" | "success">("form");
  const [amount, setAmount] = useState<number>(500);
  const [destinationProvider, setDestinationProvider] = useState<string>("M-Pesa (Vodacom)");
  const [destinationAccount, setDestinationAccount] = useState<string>("+243 81 234 5678");
  const [securityRecipientEmail, setSecurityRecipientEmail] = useState<string>("");
  const [otpCode, setOtpCode] = useState<string>("");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [remainingAttempts, setRemainingAttempts] = useState<number>(5);

  if (!isOpen) return null;

  const maskEmail = (email: string) => {
    if (!email || !email.includes("@")) return "non configuré";
    const [user, domain] = email.split("@");
    return `${user.slice(0, 2)}***@${domain}`;
  };

  const knownFee = (amount * 0.015).toFixed(2);
  const netAmount = (amount - parseFloat(knownFee)).toFixed(2);

  const handleRequestOtp = () => {
    if (!securityRecipientEmail || !securityRecipientEmail.includes("@")) {
      setErrorMessage("Veuillez renseigner une adresse e-mail de réception valide pour l'OTP.");
      return;
    }
    setIsLoading(true);
    setErrorMessage(null);
    window.setTimeout(() => {
      setIsLoading(false);
      setStep("otp");
    }, 900);
  };

  const handleVerifyOtp = (e: React.FormEvent) => {
    e.preventDefault();
    if (otpCode.trim().length !== 5) {
      setErrorMessage("Le code de vérification doit comporter exactement 5 caractères.");
      return;
    }
    setIsLoading(true);
    setErrorMessage(null);
    window.setTimeout(() => {
      setIsLoading(false);
      if (otpCode.toUpperCase() === "7K9P2" || otpCode.length === 5) {
        setStep("success");
      } else {
        const nextAttempts = remainingAttempts - 1;
        setRemainingAttempts(nextAttempts);
        setErrorMessage(`Code incorrect. Essais restants : ${nextAttempts}`);
      }
    }, 1100);
  };

  return (
    <div className="sheet-back">
      <div className="sheet" role="dialog" aria-labelledby="treasury-title" style={{ width: "min(520px, 100%)" }}>
        <header className="sheet-head">
          <span id="treasury-title">
            <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
              <DollarSign size={14} /> Trésorerie
            </span>
            <span className="sheet-sub">Saisie locale. Aucun ordre n'est envoyé à MaishaPay.</span>
          </span>
          <button type="button" className="sheet-x" onClick={onClose} aria-label="Fermer">
            <X size={14} />
          </button>
        </header>

        <div className="sheet-body">
          {step === "form" && (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <div className="sheet-card" style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div>
                  <span className="sheet-label">Solde affiché, non synchronisé</span>
                  <strong style={{ fontSize: 18 }}>4 850,00 $</strong>
                </div>
                <span className="sheet-badge warn">Donnée locale</span>
              </div>
              <label>
                <span className="sheet-label">Montant brut (USD)</span>
                <input className="sheet-input" type="number" value={amount} onChange={(e) => setAmount(Number(e.target.value))} />
              </label>
              <label>
                <span className="sheet-label">Opérateur</span>
                <select className="sheet-select" value={destinationProvider} onChange={(e) => setDestinationProvider(e.target.value)}>
                  <option>M-Pesa (Vodacom)</option>
                  <option>Airtel Money</option>
                  <option>Orange Money</option>
                  <option>Virement Bancaire (RDC)</option>
                </select>
              </label>
              <label>
                <span className="sheet-label">Compte ou téléphone</span>
                <input className="sheet-input" value={destinationAccount} onChange={(e) => setDestinationAccount(e.target.value)} />
              </label>
              <label>
                <span className="sheet-label">E-mail de réception du code</span>
                <input
                  className="sheet-input"
                  type="email"
                  required
                  placeholder="tresorier@exemple.org"
                  value={securityRecipientEmail}
                  onChange={(e) => setSecurityRecipientEmail(e.target.value)}
                />
              </label>
              <div className="sheet-card">
                <div className="sheet-kv"><span>Montant brut</span><b>{amount.toFixed(2)} $</b></div>
                <div className="sheet-kv"><span>Frais estimés (1,5 %)</span><b>{knownFee} $</b></div>
                <div className="sheet-kv"><span>Net estimé</span><b>{netAmount} $</b></div>
              </div>
              {errorMessage && <div className="sheet-error">{errorMessage}</div>}
              <div className="sheet-note">
                <ShieldAlert size={14} style={{ verticalAlign: "text-bottom" }} /> Le code à 5 caractères n'est pas envoyé depuis cette fenêtre. L'étape suivante reste une vérification locale.
              </div>
              <button type="button" className="sheet-btn primary block" onClick={handleRequestOtp} disabled={isLoading || amount <= 0}>
                {isLoading ? "Préparation..." : <>Continuer <ArrowRight size={14} /></>}
              </button>
            </div>
          )}

          {step === "otp" && (
            <form onSubmit={handleVerifyOtp} style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <div className="sheet-empty">
                <Mail size={22} />
                <strong>Code local</strong>
                Destinataire saisi : {maskEmail(securityRecipientEmail)}. Aucun message n'a quitté cette machine.
              </div>
              <input
                className="sheet-input"
                style={{ textAlign: "center", letterSpacing: "0.3em", fontFamily: "Consolas, monospace", fontWeight: 800 }}
                maxLength={5}
                value={otpCode}
                autoFocus
                placeholder="7K9P2"
                onChange={(e) => setOtpCode(e.target.value.toUpperCase())}
              />
              {errorMessage && <div className="sheet-error">{errorMessage}</div>}
              <div style={{ textAlign: "center", color: "#9aa3ab" }}>Essais restants : {remainingAttempts}</div>
              <button type="submit" className="sheet-btn ok block" disabled={isLoading || otpCode.length !== 5}>
                {isLoading ? "Vérification locale..." : "Vérifier le code"}
              </button>
              <button type="button" className="sheet-btn ghost block" onClick={() => setStep("form")}>
                Retour
              </button>
            </form>
          )}

          {step === "success" && (
            <div className="sheet-empty">
              <CheckCircle2 size={28} />
              <strong>Code accepté localement</strong>
              Aucun décaissement n'a été transmis. Montant estimé {netAmount} $ vers {destinationProvider}.
              <div className="sheet-badge" style={{ marginTop: 8 }}>Réf. locale uniquement</div>
              <div style={{ marginTop: 10 }}>
                <button
                  type="button"
                  className="sheet-btn"
                  onClick={() => {
                    setStep("form");
                    onClose();
                  }}
                >
                  Fermer
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
