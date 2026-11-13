import React, { useState } from "react";
import { X, Settings, Radio, Video, Sliders, Database } from "lucide-react";

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({ isOpen, onClose }) => {
  const [activeTab, setActiveTab] = useState<"stream" | "video" | "audio" | "storage">("stream");
  const [youtubeKey, setYoutubeKey] = useState("");
  const [facebookKey, setFacebookKey] = useState("");
  const [tiktokKey, setTiktokKey] = useState("");
  const [multistreamEnabled, setMultistreamEnabled] = useState(true);

  if (!isOpen) return null;

  return (
    <div className="sheet-back">
      <div className="sheet" role="dialog" aria-labelledby="settings-title">
        <header className="sheet-head">
          <span id="settings-title" style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
            <Settings size={14} /> Paramètres d'EECC Studio
          </span>
          <button type="button" className="sheet-x" onClick={onClose} aria-label="Fermer">
            <X size={14} />
          </button>
        </header>

        <div className="sheet-split">
          <nav className="sheet-side" aria-label="Sections des paramètres">
            <button type="button" className={`sheet-tab ${activeTab === "stream" ? "on" : ""}`} onClick={() => setActiveTab("stream")}>
              <Radio size={13} /> Diffusion & Clés
            </button>
            <button type="button" className={`sheet-tab ${activeTab === "video" ? "on" : ""}`} onClick={() => setActiveTab("video")}>
              <Video size={13} /> Vidéo & Encodeurs
            </button>
            <button type="button" className={`sheet-tab ${activeTab === "audio" ? "on" : ""}`} onClick={() => setActiveTab("audio")}>
              <Sliders size={13} /> Audio Professionnel
            </button>
            <button type="button" className={`sheet-tab ${activeTab === "storage" ? "on" : ""}`} onClick={() => setActiveTab("storage")}>
              <Database size={13} /> Supabase Storage
            </button>
          </nav>

          <div className="sheet-body">
            {activeTab === "stream" && (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <h4>Destinations RTMP</h4>
                  <label style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                    <input type="checkbox" checked={multistreamEnabled} onChange={(e) => setMultistreamEnabled(e.target.checked)} />
                    Multidiffusion simultanée
                  </label>
                </div>
                {[
                  { name: "YouTube Live", value: youtubeKey, set: setYoutubeKey, placeholder: "Clé de flux YouTube...", badge: "Non connecté" },
                  { name: "Facebook Live", value: facebookKey, set: setFacebookKey, placeholder: "Clé de flux Facebook...", badge: "Non connecté" },
                  { name: "TikTok Live", value: tiktokKey, set: setTiktokKey, placeholder: "Clé de flux TikTok...", badge: "Optionnel" },
                ].map((row) => (
                  <div key={row.name} className="sheet-card" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <strong>{row.name}</strong>
                      <span className="sheet-badge">{row.badge}</span>
                    </div>
                    <input className="sheet-input" type="password" value={row.value} placeholder={row.placeholder} onChange={(e) => row.set(e.target.value)} />
                  </div>
                ))}
              </div>
            )}

            {activeTab === "video" && (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <h4>Encodage et rendu</h4>
                <div className="sheet-grid">
                  <label>
                    <span className="sheet-label">Résolution de base</span>
                    <select className="sheet-select" defaultValue="1920x1080 (16:9 FHD)">
                      <option>1920x1080 (16:9 FHD)</option>
                      <option>1280x720 (16:9 HD)</option>
                    </select>
                  </label>
                  <label>
                    <span className="sheet-label">Cadence cible</span>
                    <select className="sheet-select" defaultValue="60 FPS">
                      <option>60 FPS</option>
                      <option>30 FPS</option>
                    </select>
                  </label>
                  <label>
                    <span className="sheet-label">Encodeur</span>
                    <select className="sheet-select" defaultValue="nvenc">
                      <option value="nvenc">NVIDIA NVENC H.264</option>
                      <option value="qsv">Intel QuickSync H.264</option>
                      <option value="x264">x264 logiciel</option>
                    </select>
                  </label>
                  <label>
                    <span className="sheet-label">Débit cible (kbps)</span>
                    <input className="sheet-input" type="number" defaultValue={4500} />
                  </label>
                </div>
                <p className="sheet-note">Ces valeurs restent locales. Aucune destination n'est ouverte tant que le statut de la régie est hors antenne.</p>
              </div>
            )}

            {activeTab === "audio" && (
              <div className="sheet-empty">
                <strong>Aucun périphérique audio détecté</strong>
                Les niveaux se règlent sur les faders du mixeur. Cette fenêtre ne mesure pas de signal.
              </div>
            )}

            {activeTab === "storage" && (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <h4>Stockage Supabase</h4>
                <div className="sheet-card" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  <div className="sheet-kv">
                    <span>Projet</span>
                    <b>rwojdiomftillhsamjal</b>
                  </div>
                  {["podcasts", "documents", "raw-media"].map((bucket) => (
                    <div key={bucket} className="sheet-kv">
                      <span>Bucket {bucket}</span>
                      <span className="sheet-badge">Non vérifié</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        <footer className="sheet-foot">
          <button type="button" className="sheet-btn ghost" onClick={onClose}>Annuler</button>
          <button type="button" className="sheet-btn primary" onClick={onClose}>Enregistrer et fermer</button>
        </footer>
      </div>
    </div>
  );
};
