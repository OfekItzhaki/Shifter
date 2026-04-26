import { apiClient } from "./client";

/**
 * Uploads an image file to the API and returns the public URL.
 * Works for profile photos, group images, space logos, etc.
 */
export async function uploadImage(file: File): Promise<string> {
  const form = new FormData();
  form.append("file", file);

  const { data } = await apiClient.post<{ url: string }>("/uploads/image", form, {
    headers: { "Content-Type": "multipart/form-data" },
  });

  return data.url;
}
