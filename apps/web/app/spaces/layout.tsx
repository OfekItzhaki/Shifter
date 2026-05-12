import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export default async function SpacesLayout({ children }: { children: React.ReactNode }) {
  const cookieStore = await cookies();
  const token = cookieStore.get("access_token")?.value;

  if (!token) {
    redirect("/login");
  }

  return <>{children}</>;
}
