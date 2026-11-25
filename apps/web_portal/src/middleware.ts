import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

export async function middleware(request: NextRequest) {
  let supabaseResponse = NextResponse.next({
    request,
  });

  const supabase = createServerClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    {
      cookies: {
        get(name: string) {
          return request.cookies.get(name)?.value;
        },
        set(name: string, value: string, options: CookieOptions) {
          supabaseResponse.cookies.set({
            name,
            value,
            ...options,
          });
        },
        remove(name: string, options: CookieOptions) {
          supabaseResponse.cookies.set({
            name,
            value: "",
            ...options,
          });
        },
      },
    }
  );

  // Récupérer la session utilisateur
  const {
    data: { user },
  } = await supabase.auth.getUser();

  // Vérifier le rôle de l'utilisateur pour les routes admin
  const isAdminRoute = request.nextUrl.pathname.startsWith("/admin");
  const isAuthRoute = request.nextUrl.pathname === "/admin/login";

  if (isAdminRoute && !isAuthRoute) {
    if (!user) {
      // Rediriger vers la page de login admin
      return NextResponse.redirect(new URL("/admin/login", request.url));
    }

    // Vérifier le rôle via la table user_roles (l'utilisateur doit avoir un rôle autorisé)
    const { data: userRole } = await supabase
      .from("user_roles")
      .select("role")
      .eq("user_id", user.id)
      .single();

    const allowedRoles = ["admin", "treasurer", "producer", "editor"];
    if (!userRole || !allowedRoles.includes(userRole.role)) {
      return NextResponse.redirect(new URL("/admin/login?error=unauthorized", request.url));
    }
  }

  // Si l'utilisateur est connecté et tente d'accéder à /admin/login, rediriger vers le dashboard
  if (isAuthRoute && user) {
    return NextResponse.redirect(new URL("/admin", request.url));
  }

  return supabaseResponse;
}

export const config = {
  matcher: ["/admin/:path*"],
};
