import React from "react";
import { X, Smartphone } from "lucide-react";

interface CameraPairingModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const CameraPairingModal: React.FC<CameraPairingModalProps> = ({ isOpen, onClose }) => {
  if (!isOpen) return null;

  return (
    <div className="sheet-back">
      <div className="sheet narrow" role="dialog" aria-labelledby="pair-title">
        <header className="sheet-head">
          <span id="pair-title" style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
            <Smartphone size={14} /> Appairer une caméra
          </span>
          <button type="button" className="sheet-x" onClick={onClose} aria-label="Fermer">
            <X size={14} />
          </button>
        </header>
        <div className="sheet-body">
          <p>
            Cette fenêtre Electron n’écoute pas le réseau et n’affiche pas de QR. L’appairage des téléphones
            est dans EECC Studio natif (Windows) : « Connecter un téléphone ».
          </p>
          <p style={{ marginTop: 12 }}>
            Le serveur y limite les téléphones à 15, en plus des caméras physiques. Aucune adresse ni aucun
            jeton n’est inventé ici.
          </p>
        </div>
        <footer className="sheet-foot">
          <button type="button" className="sheet-btn" onClick={onClose}>Fermer</button>
        </footer>
      </div>
    </div>
  );
};
