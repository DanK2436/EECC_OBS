import React, { useState } from "react";
import { X, FileAudio, UploadCloud, CheckCircle, Database } from "lucide-react";

interface PostProductionModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const PostProductionModal: React.FC<PostProductionModalProps> = ({ isOpen, onClose }) => {
  const [sermonTitle, setSermonTitle] = useState("La Puissance de la Foi Agissante");
  const [speaker, setSpeaker] = useState("Pasteur Principal");
  const [scripture, setScripture] = useState("Hébreux 11:1-6");
  const [summary, setSummary] = useState(
    "Dans ce culte, la prédication a souligné comment la foi active transforme les impossibilités en réalités selon la promesse de Dieu."
  );
  const [isExporting, setIsExporting] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);

  if (!isOpen) return null;

  const handlePublish = () => {
    setIsExporting(true);
    window.setTimeout(() => {
      setIsExporting(false);
      setIsSuccess(true);
    }, 1500);
  };

  return (
    <div className="sheet-back">
      <div className="sheet" role="dialog" aria-labelledby="post-title" style={{ width: "min(560px, 100%)" }}>
        <header className="sheet-head">
          <span id="post-title" style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
            <FileAudio size={14} /> Postproduction du culte
          </span>
          <button type="button" className="sheet-x" onClick={onClose} aria-label="Fermer">
            <X size={14} />
          </button>
        </header>
        <div className="sheet-body">
          {isSuccess ? (
            <div className="sheet-empty">
              <CheckCircle size={28} />
              <strong>Fiche préparée localement</strong>
              Aucun fichier n'a été envoyé vers Supabase. Le titre « {sermonTitle} » reste dans cette fenêtre.
              <div style={{ marginTop: 10 }}>
                <button
                  type="button"
                  className="sheet-btn primary"
                  onClick={() => {
                    setIsSuccess(false);
                    onClose();
                  }}
                >
                  Terminer
                </button>
              </div>
            </div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <label>
                <span className="sheet-label">Titre de la prédication</span>
                <input className="sheet-input" value={sermonTitle} onChange={(e) => setSermonTitle(e.target.value)} />
              </label>
              <div className="sheet-grid">
                <label>
                  <span className="sheet-label">Orateur</span>
                  <input className="sheet-input" value={speaker} onChange={(e) => setSpeaker(e.target.value)} />
                </label>
                <label>
                  <span className="sheet-label">Références bibliques</span>
                  <input className="sheet-input" value={scripture} onChange={(e) => setScripture(e.target.value)} />
                </label>
              </div>
              <label>
                <span className="sheet-label">Résumé éditable</span>
                <textarea className="sheet-input" rows={3} value={summary} onChange={(e) => setSummary(e.target.value)} />
              </label>
              <div className="sheet-card">
                <strong style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                  <Database size={13} /> Copies locales prévues
                </strong>
                <div className="sheet-grid" style={{ marginTop: 6 }}>
                  {["podcasts (MP3)", "documents", "raw-media", "EECC TV"].map((item) => (
                    <label key={item} style={{ display: "flex", gap: 6, alignItems: "center" }}>
                      <input type="checkbox" defaultChecked /> {item}
                    </label>
                  ))}
                </div>
              </div>
              <button type="button" className="sheet-btn primary block" onClick={handlePublish} disabled={isExporting}>
                <UploadCloud size={14} />
                {isExporting ? "Préparation locale..." : "Préparer l'export"}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
