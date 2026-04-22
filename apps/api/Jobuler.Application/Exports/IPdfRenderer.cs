using Jobuler.Application.Exports.Commands;

namespace Jobuler.Application.Exports;

/// <summary>
/// Contract for PDF rendering. Defined in Application, implemented in Infrastructure
/// so QuestPDF stays out of the Application layer.
/// </summary>
public interface IPdfRenderer
{
    byte[] Render(SchedulePdfModel model);
}
