import { createClient } from "@supabase/supabase-js";

const supabaseUrl =
  process.env.NEXT_PUBLIC_SUPABASE_URL || "https://rwojdiomftillhsamjal.supabase.co";
const supabaseAnonKey =
  process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY || "sb_publishable_GjqG64b82uXaDQzGJmo3Aw_pIcpjxKb";

export const supabase = createClient(supabaseUrl, supabaseAnonKey);
