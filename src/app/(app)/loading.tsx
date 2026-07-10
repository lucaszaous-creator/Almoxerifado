export default function Loading() {
  return (
    <div className="mx-auto max-w-6xl animate-pulse space-y-5">
      <div className="h-8 w-52 rounded-lg bg-slate-200" />
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        {Array.from({ length: 5 }).map((_, k) => (
          <div key={k} className="h-24 rounded-xl bg-slate-200" />
        ))}
      </div>
      <div className="grid gap-6 lg:grid-cols-2">
        <div className="h-56 rounded-xl bg-slate-200" />
        <div className="h-56 rounded-xl bg-slate-200" />
      </div>
      <div className="h-40 rounded-xl bg-slate-200" />
    </div>
  );
}
