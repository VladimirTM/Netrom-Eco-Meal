import { http } from "../base/http";

export type UploadSubfolder = "businesses" | "packages";

// Replaces the Blazor InputFile handlers in BusinessForm.razor/PackageForm.razor — the file now
// travels as multipart form data instead of an in-process IImageUploadService call.
export const uploadsApi = {
  upload: (subfolder: UploadSubfolder, file: File): Promise<string> => {
    const formData = new FormData();
    formData.append("file", file);
    return http.postForm<string>(`/uploads/${subfolder}`, formData);
  },
};
