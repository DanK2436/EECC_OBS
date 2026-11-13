import React from "react";
import { X, Smartphone } from "lucide-react";

interface CameraPairingModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const CameraPairingModal: React.FC<CameraPairingModalProps> = ({ isOpen, onClose }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-md shadow-2xl border border-slate-200 overflow-hidden">
        <div className="px-6 py-4 bg-slate-900 text-white flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <Smartphone className="w-5 h-5 text-blue-400" />
            <h3 className="font-bold text-sm">Appairer une Caméra Mobile</h3>
          </div>
          <button onClick={onClose} className="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="p-6 text-sm text-slate-700 leading-relaxed">
          <p>
            Cette fenêtre Electron n’écoute pas le réseau et n’affiche pas de QR. L’appairage des téléphones
            est dans EECC Studio natif (Windows) : « Connecter un téléphone ».
          </p>
          <p className="mt-3">
            Le serveur y limite les téléphones à 15, en plus des caméras physiques. Aucune adresse ni aucun
            jeton n’est inventé ici.
          </p>
        </div>
      </div>
    </div>
  );
};
