import React from "react";
import { Radio, Disc, Settings, DollarSign, SplitSquareVertical, FileAudio } from "lucide-react";

interface ControlsPanelProps {
  isLive: boolean;
  isRecording: boolean;
  studioMode: boolean;
  onToggleLive: () => void;
  onToggleRecording: () => void;
  onToggleStudioMode: () => void;
  onOpenTreasury: () => void;
  onOpenPostProd: () => void;
  onOpenSettings: () => void;
}

export const ControlsPanel: React.FC<ControlsPanelProps> = ({
  isLive,
  isRecording,
  studioMode,
  onToggleLive,
  onToggleRecording,
  onToggleStudioMode,
  onOpenTreasury,
  onOpenPostProd,
  onOpenSettings,
}) => {
  return (
    <div className="w-56 bg-surface flex flex-col h-full select-none">
      {/* En-tête Panneau */}
      <div className="h-8 px-3 bg-panel border-b border-border flex items-center justify-between">
        <span className="text-xs font-bold text-slate-700">Contrôles Régie</span>
      </div>

      {/* Boutons d'Action Principaux */}
      <div className="flex-1 p-2.5 space-y-2 flex flex-col justify-between">
        <div className="space-y-2">
          {/* Bouton Direct */}
          <button
            onClick={onToggleLive}
            className={`w-full py-2.5 px-3 rounded-xl text-xs font-extrabold flex items-center justify-center space-x-2 transition-all shadow-sm ${
              isLive
                ? "bg-live text-white hover:bg-red-700 animate-pulse"
                : "bg-slate-900 text-white hover:bg-slate-800"
            }`}
          >
            <Radio className="w-4 h-4" />
            <span>{isLive ? "ARRÊTER LE DIRECT" : "DÉMARRER LE DIRECT"}</span>
          </button>

          {/* Bouton Enregistrement */}
          <button
            onClick={onToggleRecording}
            className={`w-full py-2.5 px-3 rounded-xl text-xs font-extrabold flex items-center justify-center space-x-2 transition-all border ${
              isRecording
                ? "bg-red-50 text-red-600 border-red-200"
                : "bg-white text-slate-800 border-slate-300 hover:bg-slate-50"
            }`}
          >
            <Disc className="w-4 h-4 text-red-600" />
            <span>{isRecording ? "ARRÊTER L'ENREGISTREMENT" : "ENREGISTRER"}</span>
          </button>

          {/* Bouton Mode Studio */}
          <button
            onClick={onToggleStudioMode}
            className={`w-full py-2 px-3 rounded-xl text-xs font-semibold flex items-center justify-center space-x-2 border transition-colors ${
              studioMode
                ? "bg-primary/10 text-primary border-primary/30"
                : "bg-white text-slate-700 border-slate-200 hover:bg-slate-50"
            }`}
          >
            <SplitSquareVertical className="w-3.5 h-3.5" />
            <span>Mode Studio : {studioMode ? "ACTIF" : "INACTIF"}</span>
          </button>
        </div>

        {/* Outils & Trésorerie */}
        <div className="space-y-1.5 pt-2 border-t border-slate-100">
          <button
            onClick={onOpenPostProd}
            className="w-full py-1.5 px-3 rounded-lg text-xs font-semibold text-slate-700 bg-slate-100 hover:bg-slate-200 flex items-center justify-center space-x-1.5 transition-colors"
          >
            <FileAudio className="w-3.5 h-3.5 text-indigo-600" />
            <span>Postproduction Culte</span>
          </button>

          <button
            onClick={onOpenTreasury}
            className="w-full py-1.5 px-3 rounded-lg text-xs font-semibold text-emerald-800 bg-emerald-50 hover:bg-emerald-100 border border-emerald-200 flex items-center justify-center space-x-1.5 transition-colors"
          >
            <DollarSign className="w-3.5 h-3.5 text-emerald-600" />
            <span>Trésorerie & Retraits</span>
          </button>

          <button
            onClick={onOpenSettings}
            className="w-full py-1.5 px-3 rounded-lg text-xs font-semibold text-slate-600 hover:bg-slate-100 flex items-center justify-center space-x-1.5 transition-colors"
          >
            <Settings className="w-3.5 h-3.5" />
            <span>Paramètres Studio</span>
          </button>
        </div>
      </div>
    </div>
  );
};
