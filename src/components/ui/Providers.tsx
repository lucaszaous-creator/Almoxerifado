"use client";

import { ToastProvider } from "./Toast";
import { ConfirmProvider } from "./Confirm";

export default function Providers({ children }: { children: React.ReactNode }) {
  return (
    <ToastProvider>
      <ConfirmProvider>{children}</ConfirmProvider>
    </ToastProvider>
  );
}
