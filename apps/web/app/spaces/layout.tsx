import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export default async function SpacesLayout({ children }: { children: React.ReactNode }) {
  const cookieStore = await cookies();
  const token = cookieStore.get("access_token")?.value;

  if (!token) {
    redirect("/login");
  }

  // Basic JWT expiry check
  try {
    const parts = token.split(".");
    if (parts.length === 3) {
      const payload = JSON.parse(
        Buffer.from(parts[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString()
      );
      if (payload.exp && payload.exp * 1000 < Date.now()) {
        redirect("/login");
      }
    }
  } catch {
    // Can't decode — let client handle it
  }

  return <>{children}</>;
}
