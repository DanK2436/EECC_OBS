import React from "react";
import { Church, Minus, Square, X, Save } from "lucide-react";

interface TitleBarProps {
  projectName: string;
  isSaved: boolean;
  onOpenTreasury: () => void;
  onOpenSettings: () => void;
  onOpenPairing: () => void;
  onOpenPostProd: () => void;
}

export const TitleBar: React.FC<TitleBarProps> = ({
  projectName,
  isSaved,
  onOpenTreasury,
  onOpenSettings,
  onOpenPairing,
  onOpenPostProd,
}) => {
  return (
    <div className="bg-slate-900 text-white flex flex-col border-b border-slate-800">
      {/* Barre supérieure style application native Windows */}
      <div className="h-9 px-3 flex items-center justify-between text-xs select-none">
        <div className="flex items-center space-x-2">
          <img src="/logo.png" alt="EECC Logo" className="w-5 h-5 rounded object-contain bg-white/10 p-0.5" />
          <span className="font-bold tracking-tight">EECC Studio</span>
          <span className="text-slate-500">•</span>
          <span className="text-slate-300 font-medium">{projectName}</span>
          {isSaved ? (
            <span className="text-[10px] text-green-400 bg-green-950/60 px-1.5 py-0.2 rounded border border-green-800">
              Enregistré
            </span>
          ) : (
            <span className="text-[10px] text-amber-400 bg-amber-950/60 px-1.5 py-0.2 rounded border border-amber-800">
              Modifié
            </span>
          )}
        </div>

        {/* Boutons fenêtre Windows */}
        <div className="flex items-center space-x-1 -mr-2">
          <button className="w-9 h-7 flex items-center justify-center hover:bg-slate-800 text-slate-400 hover:text-white transition-colors">
            <Minus className="w-3.5 h-3.5" />
          </button>
          <button className="w-9 h-7 flex items-center justify-center hover:bg-slate-800 text-slate-400 hover:text-white transition-colors">
            <Square className="w-3 h-3" />
          </button>
          <button className="w-9 h-7 flex items-center justify-center hover:bg-red-600 text-slate-400 hover:text-white transition-colors">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Barre de menu classique de régie */}
      <div className="h-8 px-2 flex items-center space-x-1 text-xs bg-slate-800/80 border-t border-slate-800/60 text-slate-300">
        <button className="px-2.5 py-1 rounded hover:bg-slate-700/60 hover:text-white transition-colors">Fichier</button>
        <button className="px-2.5 py-1 rounded hover:bg-slate-700/60 hover:text-white transition-colors">Édition</button>
        <button className="px-2.5 py-1 rounded hover:bg-slate-700/60 hover:text-white transition-colors">Affichage</button>
        <button className="px-2.5 py-1 rounded hover:bg-slate-700/60 hover:text-white transition-colors">Collection de Scènes</button>
        
        {/* Actions Rapides Métier */}
        <button
          onClick={onOpenPairing}
          className="px-2.5 py-1 rounded bg-primary/20 text-blue-300 hover:bg-primary/30 transition-colors font-semibold"
        >
          + Appairer Caméra Mobile
        </button>
        <button
          onClick={onOpenPostProd}
          className="px-2.5 py-1 rounded bg-indigo-900/40 text-indigo-300 hover:bg-indigo-900/60 transition-colors font-semibold"
        >
          Postproduction Culte
        </button>
        <button
          onClick={onOpenTreasury}
          className="px-2.5 py-1 rounded bg-emerald-950 text-emerald-300 border border-emerald-800 hover:bg-emerald-900 transition-colors font-semibold"
        >
          Trésorerie & Dons
        </button>
        <button
          onClick={onOpenSettings}
          className="px-2.5 py-1 rounded hover:bg-slate-700/60 hover:text-white transition-colors"
        >
          Paramètres
        </button>
      </div>
    </div>
  );
};
