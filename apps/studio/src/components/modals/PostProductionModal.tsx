import React, { useState } from "react";
import {
  X,
  FileAudio,
  FileText,
  UploadCloud,
  CheckCircle,
  Share2,
  Database,
} from "lucide-react";

interface PostProductionModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const PostProductionModal: React.FC<PostProductionModalProps> = ({
  isOpen,
  onClose,
}) => {
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
    // Simulation upload vers Supabase Storage (Buckets: podcasts & documents)
    setTimeout(() => {
      setIsExporting(false);
      setIsSuccess(true);
    }, 1500);
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 select-none">
      <div className="bg-white rounded-2xl w-full max-w-xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col">
        {/* En-tête */}
        <div className="px-6 py-4 bg-slate-900 text-white flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <FileAudio className="w-5 h-5 text-indigo-400" />
            <h3 className="font-bold text-sm">Postproduction & Publication du Culte</h3>
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Corps */}
        <div className="p-6 space-y-4 text-xs">
          {isSuccess ? (
            <div className="text-center py-6 space-y-3">
              <div className="w-14 h-14 bg-green-100 text-green-600 rounded-full flex items-center justify-center mx-auto">
                <CheckCircle className="w-8 h-8" />
              </div>
              <h4 className="text-base font-bold text-textMain">Publication Réussie sur Supabase Storage !</h4>
              <p className="text-slate-600 max-w-sm mx-auto">
                Le podcast audio MP3 a été téléversé dans le bucket <code>podcasts</code> et les notes dans <code>documents</code>. La fiche de culte est désormais disponible dans l’application mobile <strong>EECC TV</strong>.
              </p>
              <div className="p-2.5 bg-slate-50 border border-slate-200 rounded-lg text-slate-500 font-mono text-[10px]">
                URL: https://rwojdiomftillhsamjal.supabase.co/storage/v1/object/public/podcasts/...
              </div>
              <button
                onClick={() => {
                  setIsSuccess(false);
                  onClose();
                }}
                className="px-6 py-2 bg-primary text-white rounded-xl font-bold"
              >
                Terminer
              </button>
            </div>
          ) : (
            <>
              <div>
                <label className="block font-bold text-slate-700 mb-1">Titre de la Prédication :</label>
                <input
                  type="text"
                  value={sermonTitle}
                  onChange={(e) => setSermonTitle(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg font-bold text-slate-800"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block font-bold text-slate-700 mb-1">Orateur :</label>
                  <input
                    type="text"
                    value={speaker}
                    onChange={(e) => setSpeaker(e.target.value)}
                    className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800"
                  />
                </div>
                <div>
                  <label className="block font-bold text-slate-700 mb-1">Références Bibliques :</label>
                  <input
                    type="text"
                    value={scripture}
                    onChange={(e) => setScripture(e.target.value)}
                    className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800 font-semibold text-primary"
                  />
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1">
                  Résumé pour les fidèles (Proposition éditable) :
                </label>
                <textarea
                  rows={3}
                  value={summary}
                  onChange={(e) => setSummary(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800 text-xs leading-relaxed"
                />
              </div>

              {/* Options d'exportation vers Supabase Storage */}
              <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200 space-y-2">
                <span className="font-bold text-slate-700 flex items-center">
                  <Database className="w-3.5 h-3.5 mr-1.5 text-primary" />
                  Stockage Supabase Storage (rwojdiomftillhsamjal) :
                </span>
                <div className="grid grid-cols-2 gap-2 text-slate-600">
                  <label className="flex items-center space-x-2">
                    <input type="checkbox" defaultChecked className="accent-primary" />
                    <span>Bucket <code>podcasts</code> (MP3)</span>
                  </label>
                  <label className="flex items-center space-x-2">
                    <input type="checkbox" defaultChecked className="accent-primary" />
                    <span>Bucket <code>documents</code> (PDF/DOCX)</span>
                  </label>
                  <label className="flex items-center space-x-2">
                    <input type="checkbox" defaultChecked className="accent-primary" />
                    <span>Bucket <code>raw-media</code> (Master WAV)</span>
                  </label>
                  <label className="flex items-center space-x-2">
                    <input type="checkbox" defaultChecked className="accent-primary" />
                    <span>Synchroniser avec EECC TV</span>
                  </label>
                </div>
              </div>

              <button
                onClick={handlePublish}
                disabled={isExporting}
                className="w-full py-3 bg-primary hover:bg-primary-dark text-white rounded-xl font-bold transition-all shadow-md flex items-center justify-center space-x-2"
              >
                <UploadCloud className="w-4 h-4" />
                <span>
                  {isExporting ? "Téléversement vers Supabase Storage..." : "Lancer l'export & la publication"}
                </span>
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
