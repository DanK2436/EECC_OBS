import React, { useState } from "react";
import { X, Settings, Radio, Video, Sliders, Database, CheckCircle2 } from "lucide-react";

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({ isOpen, onClose }) => {
  const [activeTab, setActiveTab] = useState<"stream" | "video" | "audio" | "storage">("stream");

  // Destinations RTMP
  const [youtubeKey, setYoutubeKey] = useState("");
  const [facebookKey, setFacebookKey] = useState("");
  const [tiktokKey, setTiktokKey] = useState("");
  const [multistreamEnabled, setMultistreamEnabled] = useState(true);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 select-none">
      <div className="bg-white rounded-2xl w-full max-w-2xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col max-h-[85vh]">
        {/* En-tête */}
        <div className="px-6 py-4 bg-slate-900 text-white flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <Settings className="w-5 h-5 text-blue-400" />
            <h3 className="font-bold text-sm">Paramètres d'EECC Studio</h3>
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Corps avec onglets */}
        <div className="flex-1 flex overflow-hidden">
          {/* Menu Onglets */}
          <div className="w-48 bg-slate-50 border-r border-slate-200 p-2 space-y-1 text-xs">
            <button
              onClick={() => setActiveTab("stream")}
              className={`w-full px-3 py-2 rounded-lg text-left font-semibold flex items-center space-x-2 ${
                activeTab === "stream" ? "bg-primary text-white" : "text-slate-700 hover:bg-slate-100"
              }`}
            >
              <Radio className="w-3.5 h-3.5" />
              <span>Diffusion & Clés</span>
            </button>
            <button
              onClick={() => setActiveTab("video")}
              className={`w-full px-3 py-2 rounded-lg text-left font-semibold flex items-center space-x-2 ${
                activeTab === "video" ? "bg-primary text-white" : "text-slate-700 hover:bg-slate-100"
              }`}
            >
              <Video className="w-3.5 h-3.5" />
              <span>Vidéo & Encodeurs</span>
            </button>
            <button
              onClick={() => setActiveTab("audio")}
              className={`w-full px-3 py-2 rounded-lg text-left font-semibold flex items-center space-x-2 ${
                activeTab === "audio" ? "bg-primary text-white" : "text-slate-700 hover:bg-slate-100"
              }`}
            >
              <Sliders className="w-3.5 h-3.5" />
              <span>Audio Professionnel</span>
            </button>
            <button
              onClick={() => setActiveTab("storage")}
              className={`w-full px-3 py-2 rounded-lg text-left font-semibold flex items-center space-x-2 ${
                activeTab === "storage" ? "bg-primary text-white" : "text-slate-700 hover:bg-slate-100"
              }`}
            >
              <Database className="w-3.5 h-3.5" />
              <span>Supabase Storage</span>
            </button>
          </div>

          {/* Contenu de l'onglet actif */}
          <div className="flex-1 p-6 overflow-y-auto space-y-5 text-xs text-slate-700">
            {activeTab === "stream" && (
              <div className="space-y-4">
                <div className="flex items-center justify-between pb-2 border-b border-slate-100">
                  <h4 className="font-bold text-sm text-textMain">Destinations Multi-Plateformes (RTMP)</h4>
                  <label className="flex items-center space-x-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={multistreamEnabled}
                      onChange={(e) => setMultistreamEnabled(e.target.checked)}
                      className="rounded accent-primary"
                    />
                    <span className="font-semibold text-slate-700">Multidiffusion simultanée</span>
                  </label>
                </div>

                {/* YouTube */}
                <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200 space-y-2">
                  <div className="flex items-center justify-between font-bold text-slate-800">
                    <span className="flex items-center text-red-600">
                      <span className="w-2 h-2 rounded-full bg-red-600 mr-2" /> YouTube Live
                    </span>
                    <span className="text-[10px] text-slate-500 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">Non connecté</span>
                  </div>
                  <input
                    type="password"
                    value={youtubeKey}
                    onChange={(e) => setYoutubeKey(e.target.value)}
                    placeholder="Clé de flux YouTube..."
                    className="w-full px-3 py-1.5 bg-white border border-slate-200 rounded text-xs font-mono"
                  />
                </div>

                {/* Facebook */}
                <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200 space-y-2">
                  <div className="flex items-center justify-between font-bold text-slate-800">
                    <span className="flex items-center text-blue-600">
                      <span className="w-2 h-2 rounded-full bg-blue-600 mr-2" /> Facebook Live
                    </span>
                    <span className="text-[10px] text-slate-500 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">Non connecté</span>
                  </div>
                  <input
                    type="password"
                    value={facebookKey}
                    onChange={(e) => setFacebookKey(e.target.value)}
                    placeholder="Clé de flux Facebook..."
                    className="w-full px-3 py-1.5 bg-white border border-slate-200 rounded text-xs font-mono"
                  />
                </div>

                {/* TikTok */}
                <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200 space-y-2">
                  <div className="flex items-center justify-between font-bold text-slate-800">
                    <span className="flex items-center text-slate-900">
                      <span className="w-2 h-2 rounded-full bg-slate-900 mr-2" /> TikTok Live
                    </span>
                    <span className="text-[10px] text-slate-400 bg-slate-100 px-2 py-0.5 rounded">Optionnel</span>
                  </div>
                  <input
                    type="password"
                    value={tiktokKey}
                    onChange={(e) => setTiktokKey(e.target.value)}
                    placeholder="Clé de flux TikTok..."
                    className="w-full px-3 py-1.5 bg-white border border-slate-200 rounded text-xs font-mono"
                  />
                </div>
              </div>
            )}

            {activeTab === "video" && (
              <div className="space-y-4">
                <h4 className="font-bold text-sm text-textMain">Encodage & Rendu Vidéo</h4>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block font-semibold mb-1">Résolution de Base :</label>
                    <select className="w-full p-2 bg-slate-50 border border-slate-200 rounded">
                      <option>1920x1080 (16:9 FHD)</option>
                      <option>1280x720 (16:9 HD)</option>
                    </select>
                  </div>
                  <div>
                    <label className="block font-semibold mb-1">Fréquence d'Images (FPS) :</label>
                    <select className="w-full p-2 bg-slate-50 border border-slate-200 rounded">
                      <option>60 FPS (Fluide)</option>
                      <option>30 FPS (Standard)</option>
                    </select>
                  </div>
                  <div>
                    <label className="block font-semibold mb-1">Encodeur Matériel :</label>
                    <select className="w-full p-2 bg-slate-50 border border-slate-200 rounded">
                      <option>NVIDIA NVENC H.264 (Matériel)</option>
                      <option>Intel QuickSync H.264</option>
                      <option>x264 (Processeur logiciel)</option>
                    </select>
                  </div>
                  <div>
                    <label className="block font-semibold mb-1">Débit cible (Bitrate) :</label>
                    <input
                      type="number"
                      defaultValue={4500}
                      className="w-full p-2 bg-slate-50 border border-slate-200 rounded font-mono"
                    />
                  </div>
                </div>
              </div>
            )}

            {activeTab === "storage" && (
              <div className="space-y-4">
                <h4 className="font-bold text-sm text-textMain">Stockage Numérique Supabase Storage</h4>
                <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 space-y-3">
                  <div>
                    <span className="font-bold block text-textMain">Projet Supabase :</span>
                    <span className="font-mono text-slate-500">https://rwojdiomftillhsamjal.supabase.co</span>
                  </div>

                  <div className="space-y-2 pt-2 border-t border-slate-200">
                    <div className="flex items-center justify-between text-xs">
                      <span>Bucket <code>podcasts</code> (MP3 Public)</span>
                      <span className="text-green-600 font-semibold flex items-center">
                        <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Actif
                      </span>
                    </div>
                    <div className="flex items-center justify-between text-xs">
                      <span>Bucket <code>documents</code> (PDF/DOCX Public)</span>
                      <span className="text-green-600 font-semibold flex items-center">
                        <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Actif
                      </span>
                    </div>
                    <div className="flex items-center justify-between text-xs">
                      <span>Bucket <code>raw-media</code> (Master WAV Privé)</span>
                      <span className="text-green-600 font-semibold flex items-center">
                        <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> Actif
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-3 bg-slate-50 border-t border-slate-200 flex justify-end space-x-2">
          <button
            onClick={onClose}
            className="px-4 py-2 bg-primary text-white rounded-lg text-xs font-bold"
          >
            Enregistrer & Fermer
          </button>
        </div>
      </div>
    </div>
  );
};
