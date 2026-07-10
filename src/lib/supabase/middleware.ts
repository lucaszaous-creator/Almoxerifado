import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

type CookieParaDefinir = { name: string; value: string; options?: CookieOptions };

export async function atualizarSessao(request: NextRequest) {
  let response = NextResponse.next({ request });

  const supabase = createServerClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    {
      cookies: {
        getAll() {
          return request.cookies.getAll();
        },
        setAll(cookiesToSet: CookieParaDefinir[]) {
          cookiesToSet.forEach(({ name, value }) =>
            request.cookies.set(name, value)
          );
          response = NextResponse.next({ request });
          cookiesToSet.forEach(({ name, value, options }) =>
            response.cookies.set(name, value, options)
          );
        },
      },
    }
  );

  const {
    data: { user },
  } = await supabase.auth.getUser();

  const url = request.nextUrl.clone();
  const ehLogin = url.pathname.startsWith("/login");
  const ehAuth = url.pathname.startsWith("/auth");

  if (!user && !ehLogin && !ehAuth) {
    url.pathname = "/login";
    return NextResponse.redirect(url);
  }

  if (user && ehLogin) {
    url.pathname = "/";
    return NextResponse.redirect(url);
  }

  return response;
}
