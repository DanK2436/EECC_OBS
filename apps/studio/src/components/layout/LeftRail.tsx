import React from "react";
import {
  Home,
  Radio,
  Play,
  Image as ImageIcon,
  LayoutGrid,
  Wrench,
  Settings,
} from "lucide-react";

export type StudioNav =
  | "accueil"
  | "diffusion"
  | "enregistrement"
  | "playlists"
  | "medias"
  | "scenes"
  | "outils"
  | "parametres";

const ITEMS: { id: StudioNav; label: string; icon?: React.ReactNode; rec?: boolean }[] = [
  { id: "accueil", label: "Accueil", icon: <Home size={16} strokeWidth={1.75} /> },
  { id: "diffusion", label: "Diffusion", icon: <Radio size={16} strokeWidth={1.75} /> },
  { id: "enregistrement", label: "Enregistrement", rec: true },
  { id: "playlists", label: "Playlists", icon: <Play size={16} strokeWidth={1.75} /> },
  { id: "medias", label: "Médias", icon: <ImageIcon size={16} strokeWidth={1.75} /> },
  { id: "scenes", label: "Scènes", icon: <LayoutGrid size={16} strokeWidth={1.75} /> },
  { id: "outils", label: "Outils", icon: <Wrench size={16} strokeWidth={1.75} /> },
  { id: "parametres", label: "Paramètres", icon: <Settings size={16} strokeWidth={1.75} /> },
];

interface LeftRailProps {
  active: StudioNav;
  onSelect: (id: StudioNav) => void;
}

export const LeftRail: React.FC<LeftRailProps> = ({ active, onSelect }) => {
  return (
    <nav className="rail" aria-label="Navigation studio">
      {ITEMS.map((item) => (
        <button
          key={item.id}
          type="button"
          className={`rail-btn ${active === item.id ? "active" : ""}`}
          onClick={() => onSelect(item.id)}
        >
          {item.rec ? <i className="rec-dot" /> : item.icon}
          <span>{item.label}</span>
        </button>
      ))}
    </nav>
  );
};
