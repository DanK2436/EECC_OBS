import React from "react";
import { Play, Circle, Monitor, Camera, Zap, Settings } from "lucide-react";

interface ControlsPanelProps {
  emphasis: boolean;
  isLive: boolean;
  isRecording: boolean;
  studioMode: boolean;
  note: string | null;
  onToggleLive: () => void;
  onToggleRecording: () => void;
  onToggleStudioMode: () => void;
  onSnapshot: () => void;
  onQuickSettings: () => void;
}

export const ControlsPanel: React.FC<ControlsPanelProps> = ({
  emphasis,
  isLive,
  isRecording,
  studioMode,
  note,
  onToggleLive,
  onToggleRecording,
  onToggleStudioMode,
  onSnapshot,
  onQuickSettings,
}) => {
  return (
    <section className={`dock ${emphasis ? "emphasis" : ""}`} aria-label="Commandes">
      <header className="dock-head">
        <span style={{ display: "inline-flex", alignItems: "center", gap: 5 }}>
          <Settings size={13} /> Commandes
        </span>
      </header>
      <div className="dock-body">
        <div className="commands">
          <button type="button" className="cmd stream" onClick={onToggleLive}>
            <Play size={13} fill="white" /> {isLive ? "Arrêter le Streaming" : "Démarrer le Streaming"}
          </button>
          <button type="button" className="cmd record" onClick={onToggleRecording}>
            <Circle size={11} fill="#ffd0d0" /> {isRecording ? "Arrêter l'Enregistrement" : "Démarrer l'Enregistrement"}
          </button>
          <button type="button" className={`cmd studio ${studioMode ? "" : "off"}`} onClick={onToggleStudioMode}>
            <Monitor size={13} /> Mode Studio : {studioMode ? "ACTIF" : "INACTIF"}
          </button>
          <button type="button" className="cmd plain" onClick={onSnapshot}>
            <Camera size={13} /> Instantané
          </button>
          <button type="button" className="cmd plain" onClick={onQuickSettings}>
            <Zap size={13} /> Paramètres rapides
          </button>
          {note && <div className="cmd-note">{note}</div>}
        </div>
      </div>
    </section>
  );
};
