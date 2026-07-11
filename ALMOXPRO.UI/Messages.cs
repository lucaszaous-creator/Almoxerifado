using ALMOXPRO.Application.Services;

namespace ALMOXPRO.UI;

/// <summary>Navega para uma tela a partir de qualquer ViewModel (ex.: cartões do dashboard).</summary>
public record OpenScreenMessage(Type ViewModelType);

/// <summary>Abre a tela de relatórios já gerando o relatório indicado.</summary>
public record OpenReportMessage(ReportKind Kind);
