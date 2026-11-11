import { createClient } from "@supabase/supabase-js";

const supabaseUrl = "https://rwojdiomftillhsamjal.supabase.co";
const supabaseAnonKey = "sb_publishable_GjqG64b82uXaDQzGJmo3Aw_pIcpjxKb";

export const supabase = createClient(supabaseUrl, supabaseAnonKey);

export async function uploadPodcastAudio(
  file: File | Blob,
  fileName: string
): Promise<{ publicUrl: string | null; error: Error | null }> {
  try {
    const filePath = `podcasts/${new Date().getFullYear()}/${fileName}`;
    const { data, error } = await supabase.storage
      .from("podcasts")
      .upload(filePath, file, {
        cacheControl: "3600",
        upsert: true,
      });

    if (error) throw error;

    const { data: urlData } = supabase.storage
      .from("podcasts")
      .getPublicUrl(filePath);

    return { publicUrl: urlData.publicUrl, error: null };
  } catch (err) {
    return { publicUrl: null, error: err as Error };
  }
}

export async function uploadSermonDocument(
  file: File | Blob,
  fileName: string
): Promise<{ publicUrl: string | null; error: Error | null }> {
  try {
    const filePath = `documents/${new Date().getFullYear()}/${fileName}`;
    const { data, error } = await supabase.storage
      .from("documents")
      .upload(filePath, file, {
        cacheControl: "3600",
        upsert: true,
      });

    if (error) throw error;

    const { data: urlData } = supabase.storage
      .from("documents")
      .getPublicUrl(filePath);

    return { publicUrl: urlData.publicUrl, error: null };
  } catch (err) {
    return { publicUrl: null, error: err as Error };
  }
}
