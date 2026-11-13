import React from "react";
import { Home, Radio, Image as ImageIcon, LayoutGrid, Music, Wrench, Settings, Monitor } from "lucide-react";
import type { StudioNav } from "./LeftRail";

interface TitleBarProps {
  active: StudioNav;
  onSelect: (id: StudioNav) => void;
  screenLabel: string;
  onScreen: (label: string) => void;
  recording: boolean;
  studioMode: boolean;
  onToggleStudioMode: () => void;
  demo: boolean;
}

const TABS: { id: StudioNav; label: string; icon: React.ReactNode }[] = [
  { id: "accueil", label: "Accueil", icon: <Home size={12} strokeWidth={2} /> },
  { id: "diffusion", label: "Diffusion", icon: <Radio size={12} strokeWidth={2} /> },
  { id: "medias", label: "Médias", icon: <ImageIcon size={12} strokeWidth={2} /> },
  { id: "scenes", label: "Scènes", icon: <LayoutGrid size={12} strokeWidth={2} /> },
  { id: "playlists", label: "Playlists", icon: <Music size={12} strokeWidth={2} /> },
  { id: "outils", label: "Outils", icon: <Wrench size={12} strokeWidth={2} /> },
  { id: "parametres", label: "Paramètres", icon: <Settings size={12} strokeWidth={2} /> },
];

export const TitleBar: React.FC<TitleBarProps> = ({
  active,
  onSelect,
  screenLabel,
  onScreen,
  recording,
  studioMode,
  onToggleStudioMode,
  demo,
}) => {
  return (
    <header className="titlebar" data-demo={demo ? "1" : "0"}>
      <div className="brand">
        <div className="brand-mark" aria-hidden>
          <svg width="14" height="14" viewBox="0 0 14 14">
            <path d="M6 1h2v4h4v2H8v4H6V7H2V5h4z" fill="#f4f4f4" />
          </svg>
        </div>
        <div>
          <div className="brand-name">ECC Studio</div>
          <div className="brand-sub">Radio &amp; Diffusion Pro</div>
        </div>
      </div>

      <div className="top-tabs">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={`top-tab ${active === tab.id ? "active" : ""}`}
            onClick={() => onSelect(tab.id)}
          >
            {tab.icon}
            <span className="lbl">{tab.label}</span>
          </button>
        ))}
      </div>

      <div className="title-controls">
        <select
          className="screen-select"
          aria-label="Écran de sortie"
          value={screenLabel}
          onChange={(e) => onScreen(e.target.value)}
        >
          <option>Écran principal</option>
          <option>Moniteur régie 2</option>
          <option>Sortie HDMI / SDI</option>
        </select>
        <div className={`rec-badge ${recording ? "on" : ""}`} title={recording ? "Enregistrement en cours" : "Aucun enregistrement"}>
          <i />
          {recording ? "REC : ON" : "REC : OFF"}
        </div>
        <button type="button" className={`mode-studio-btn ${studioMode ? "" : "off"}`} onClick={onToggleStudioMode}>
          <Monitor size={12} />
          Mode Studio
        </button>
        <button type="button" className="win-btn" aria-label="Réduire" title="Réduire">
          —
        </button>
        <button type="button" className="win-btn" aria-label="Agrandir" title="Agrandir">
          ▢
        </button>
        <button type="button" className="win-btn close" aria-label="Fermer" title="Fermer" onClick={() => window.close()}>
          ✕
        </button>
      </div>
    </header>
  );
};
