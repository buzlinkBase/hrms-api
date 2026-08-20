using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

public class Employee201Document : IDocument
{
    private readonly EmployeeFullModel _e;

    private static readonly string Primary = "#1DA081";
    private static readonly string SectionHeaderBg = "#f5f5f5";
    private static readonly string TableHeaderBg = "#e8f5f1";
    private static readonly string BorderColor = "#d9d9d9";
    private static readonly string LabelColor = "#666666";
    private static readonly string TextColor = "#1a1a1a";

    public Employee201Document(EmployeeFullModel employee) => _e = employee;

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"201 File - {_e.FullName}",
        Author = "One Punch HRIS",
        CreationDate = DateTimeOffset.UtcNow,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(1.5f, Unit.Centimetre);
            page.MarginBottom(1.5f, Unit.Centimetre);
            page.MarginHorizontal(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextColor));

            page.Header().Element(ComposePageHeader);
            page.Content().PaddingTop(8).Element(ComposeContent);
            page.Footer().AlignCenter().PaddingTop(4).Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(LabelColor));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }

    void ComposePageHeader(IContainer c)
    {
        c.BorderBottom(1).BorderColor(Primary).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("ONE PUNCH HRIS").Bold().FontSize(12).FontColor(Primary);
                col.Item().Text("EMPLOYEE 201 FILE").FontSize(9).FontColor(LabelColor).LetterSpacing(1);
            });
            row.ConstantItem(160).AlignRight().Column(col =>
            {
                col.Item().Text(_e.FullName ?? $"{_e.FirstName} {_e.LastName}").Bold().FontSize(11);
                col.Item().Text($"#{_e.EmployeeNo}").FontSize(9).FontColor(LabelColor);
                col.Item().Text(_e.EmploymentStatus.ToString()).FontSize(9).FontColor(Primary);
            });
        });
    }

    void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            col.Spacing(8);

            col.Item().Element(ComposePersonalInfo);
            col.Item().Element(ComposeEmploymentDetails);
            col.Item().Element(ComposeCompensation);
            col.Item().Element(ComposeGovernmentNumbers);

            if (_e.Educations?.Any() == true)
                col.Item().Element(ComposeEducation);
            if (_e.Skills?.Any() == true)
                col.Item().Element(ComposeSkills);
            if (_e.Dependents?.Any() == true)
                col.Item().Element(ComposeDependents);
            if (_e.Employments?.Any() == true)
                col.Item().Element(ComposeEmploymentHistory);
            if (_e.Assets?.Any() == true)
                col.Item().Element(ComposeAssets);
            if (_e.EmployeeRecords?.Any() == true)
                col.Item().Element(ComposeDocuments);
        });
    }

    // ── Section helpers ─────────────────────────────────────────────────────

    void SectionHeader(IContainer c, string title) =>
        c.Background(SectionHeaderBg)
         .BorderBottom(1).BorderColor(BorderColor)
         .Padding(5)
         .Text(title).Bold().FontSize(9).FontColor(Primary);

    void LabelValue(IContainer c, string label, string? value) =>
        c.Padding(3).Row(row =>
        {
            row.ConstantItem(110).Text(label).FontColor(LabelColor);
            row.RelativeItem().Text(value ?? "—");
        });

    void TwoColRow(ColumnDescriptor col, (string label, string? value) left, (string label, string? value) right) =>
        col.Item().Row(row =>
        {
            row.RelativeItem().Element(c => LabelValue(c, left.label, left.value));
            row.RelativeItem().Element(c => LabelValue(c, right.label, right.value));
        });

    // ── Sections ─────────────────────────────────────────────────────────────

    void ComposePersonalInfo(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "PERSONAL INFORMATION"));
            TwoColRow(col,
                ("Full Name", _e.FullName ?? $"{_e.LastName}, {_e.FirstName} {_e.MiddleName}"),
                ("Date of Birth", _e.DOB?.ToString("MMM dd, yyyy") ?? "—"));
            TwoColRow(col,
                ("Gender", _e.Gender),
                ("Age", _e.Age > 0 ? _e.Age.ToString() : "—"));
            TwoColRow(col,
                ("Civil Status", _e.CivilStatus),
                ("Blood Type", _e.BloodType));
            col.Item().Element(c2 => LabelValue(c2, "Contact No.", _e.Contact));
            col.Item().Element(c2 => LabelValue(c2, "Address 1", _e.Address1));
            col.Item().Element(c2 => LabelValue(c2, "Address 2", _e.Address2));
        });
    }

    void ComposeEmploymentDetails(IContainer c)
    {
        var hireDate = _e.HireDate != DateOnly.MinValue ? _e.HireDate.ToString("MMM dd, yyyy") : "—";
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "EMPLOYMENT DETAILS"));
            TwoColRow(col,
                ("Employee No.", _e.EmployeeNo),
                ("Hire Date", hireDate));
            TwoColRow(col,
                ("Department", _e.DepartmentName),
                ("Section", "—"));
            TwoColRow(col,
                ("Position", _e.PositionName),
                ("Job Level", _e.JobLevel.ToString()));
            TwoColRow(col,
                ("Branch", _e.BranchName),
                ("Client / Site", _e.ClientName));
            TwoColRow(col,
                ("Operation Area", _e.AreaName),
                ("Hiring Entity", _e.HiringEntity));
            TwoColRow(col,
                ("Employment Status", _e.EmploymentStatus.ToString()),
                ("Status", _e.Status));
            if (_e.ContractStart.HasValue || _e.ContractEnd.HasValue)
                TwoColRow(col,
                    ("Contract Start", _e.ContractStart?.ToString("MMM dd, yyyy")),
                    ("Contract End", _e.ContractEnd?.ToString("MMM dd, yyyy")));

            var restDayNames = _e.RestDays?.Select(r => r.DayName.ToString()).ToList();
            col.Item().Element(c2 => LabelValue(c2, "Rest Days",
                restDayNames?.Any() == true ? string.Join(", ", restDayNames) : "—"));
            col.Item().Element(c2 => LabelValue(c2, "Time Shift", _e.TimeShiftName));
            col.Item().Element(c2 => LabelValue(c2, "Payroll Group", _e.PayrollGroupName));
        });
    }

    void ComposeCompensation(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "COMPENSATION / PAYROLL"));
            TwoColRow(col,
                ("Salary Type", _e.SalaryType.ToString()),
                ("Payroll Frequency", _e.PayrollFrequency.ToString()));
            TwoColRow(col,
                ("Monthly Rate", _e.MonthlyRate > 0 ? _e.MonthlyRate.ToString("N2") : "—"),
                ("Daily Rate", _e.DailyRate > 0 ? _e.DailyRate.ToString("N2") : "—"));
            TwoColRow(col,
                ("COLA (per payroll)", _e.Cola > 0 ? _e.Cola.ToString("N2") : "—"),
                ("Mode of Payment", _e.ModeOfPayment.ToString()));
            TwoColRow(col,
                ("Bank Name", _e.BankName),
                ("Bank Account No.", _e.BankNo));
        });
    }

    void ComposeGovernmentNumbers(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "OTHER INCOME & DEDUCTIONS (GOVERNMENT)"));

            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(110);
                    cols.ConstantColumn(100);
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                });

                static IContainer HeaderCell(IContainer c, string bg) =>
                    c.Background(bg).Padding(3);

                table.Header(header =>
                {
                    header.Cell().Element(c2 => HeaderCell(c2, TableHeaderBg)).Text("Contribution").Bold();
                    header.Cell().Element(c2 => HeaderCell(c2, TableHeaderBg)).Text("Number").Bold();
                    header.Cell().Element(c2 => HeaderCell(c2, TableHeaderBg)).Text("Basis").Bold();
                    header.Cell().Element(c2 => HeaderCell(c2, TableHeaderBg)).Text("EE Rate").Bold();
                    header.Cell().Element(c2 => HeaderCell(c2, TableHeaderBg)).Text("ER Rate").Bold();
                    header.Cell().Element(c2 => HeaderCell(c2, TableHeaderBg)).Text("Add-Ons").Bold();
                });

                static IContainer DataCell(IContainer c) => c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);

                table.Cell().Element(DataCell).Text("SSS");
                table.Cell().Element(DataCell).Text(_e.SSSNo ?? "—");
                table.Cell().Element(DataCell).Text(_e.SSSRate?.ComputationType.ToString() ?? "—");
                table.Cell().Element(DataCell).Text(_e.SSSRate?.EE.ToString("P2") ?? "—");
                table.Cell().Element(DataCell).Text(_e.SSSRate?.ER.ToString("P2") ?? "—");
                table.Cell().Element(DataCell).Text(_e.SSSRate?.AddOns.ToString("P2") ?? "—");

                table.Cell().Element(DataCell).Text("PhilHealth");
                table.Cell().Element(DataCell).Text(_e.PHICNo ?? "—");
                table.Cell().Element(DataCell).Text(_e.PHICRate?.ComputationType.ToString() ?? "—");
                table.Cell().Element(DataCell).Text(_e.PHICRate?.EE.ToString("P2") ?? "—");
                table.Cell().Element(DataCell).Text(_e.PHICRate?.ER.ToString("P2") ?? "—");
                table.Cell().Element(DataCell).Text(_e.PHICRate?.AddOns.ToString("P2") ?? "—");

                table.Cell().Element(DataCell).Text("Pag-IBIG");
                table.Cell().Element(DataCell).Text(_e.HDMFNo ?? "—");
                table.Cell().Element(DataCell).Text(_e.HDMFRate?.ComputationType.ToString() ?? "—");
                table.Cell().Element(DataCell).Text(_e.HDMFRate?.EE.ToString("P2") ?? "—");
                table.Cell().Element(DataCell).Text(_e.HDMFRate?.ER.ToString("P2") ?? "—");
                table.Cell().Element(DataCell).Text(_e.HDMFRate?.AddOns.ToString("P2") ?? "—");

                table.Cell().Element(DataCell).Text("Tax (BIR)");
                table.Cell().Element(DataCell).Text(_e.TIN ?? "—");
                table.Cell().Element(DataCell).Text(_e.TaxRate?.ComputationType.ToString() ?? "—");
                table.Cell().Element(DataCell).Text(_e.TaxRate?.EE.ToString("P2") ?? "—");
                table.Cell().Element(DataCell).Text("—");
                table.Cell().Element(DataCell).Text(_e.TaxRate?.AddOns.ToString("P2") ?? "—");
            });
        });
    }

    void ComposeEducation(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "EDUCATION"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.ConstantColumn(100);
                });
                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("School / Institution").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Year Graduated").Bold();
                });
                foreach (var edu in _e.Educations!)
                {
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(edu.SchoolName ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(edu.YearGraduated > 0 ? edu.YearGraduated.ToString() : "—");
                }
            });
        });
    }

    void ComposeSkills(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "SKILLS"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.ConstantColumn(80);
                });
                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Skill").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Level (0–10)").Bold();
                });
                foreach (var s in _e.Skills!)
                {
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(s.Name ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(s.Level.ToString());
                }
            });
        });
    }

    void ComposeDependents(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "DEPENDENTS"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(60);
                    cols.ConstantColumn(90);
                });
                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Full Name").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Relationship").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Gender").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Date of Birth").Bold();
                });
                foreach (var d in _e.Dependents!)
                {
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(d.FullName ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(d.Relationship ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(d.Gender ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(
                        d.DOB != default ? d.DOB.ToString("MMM dd, yyyy") : "—");
                }
            });
        });
    }

    void ComposeEmploymentHistory(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "EMPLOYMENT HISTORY"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(80);
                    cols.ConstantColumn(80);
                });
                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Company").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Position").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("From").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("To").Bold();
                });
                foreach (var h in _e.Employments!)
                {
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(h.CompanyName ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(h.Position ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(
                        h.FromDate != default ? h.FromDate.ToString("MMM yyyy") : "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(
                        h.ToDate != default ? h.ToDate.ToString("MMM yyyy") : "—");
                }
            });
        });
    }

    void ComposeAssets(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "ASSIGNED ASSETS"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.ConstantColumn(85);
                    cols.ConstantColumn(50);
                    cols.ConstantColumn(70);
                    cols.ConstantColumn(70);
                });
                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Asset Type").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Description").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Serial No.").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Qty").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Issued").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Status").Bold();
                });
                foreach (var a in _e.Assets!)
                {
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(a.AssetType ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(a.AssetDescription ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(a.SerialNo ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(a.Qty.ToString());
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(
                        a.IssuanceDate != default ? a.IssuanceDate.ToString("MM/dd/yyyy") : "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(a.Status ?? "—");
                }
            });
        });
    }

    void ComposeDocuments(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "DOCUMENT ATTACHMENTS"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(3);
                });
                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Record Type").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("Description").Bold();
                    header.Cell().Background(TableHeaderBg).Padding(3).Text("File / Reference").Bold();
                });
                foreach (var r in _e.EmployeeRecords!)
                {
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(r.RecordType ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(r.Description ?? "—");
                    table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(r.File ?? "—");
                }
            });
        });
    }
}
