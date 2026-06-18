using FluentValidation;
using LifeLine.Application.DTO.Campaign;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Validators.Campaign
{
    public class CreateCampaignDtoValidator : AbstractValidator<CreateCampaignDto>
    {
        public CreateCampaignDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Campaign title is required.")
                .MinimumLength(10).WithMessage("Title must be at least 10 characters.")
                .MaximumLength(150).WithMessage("Title cannot exceed 150 characters.");

            RuleFor(x => x.PatientName)
                .NotEmpty().WithMessage("Patient name is required.")
                .MaximumLength(100).WithMessage("Patient name cannot exceed 100 characters.");

            RuleFor(x => x.MedicalCondition)
                .NotEmpty().WithMessage("Medical condition is required.")
                .MaximumLength(200).WithMessage("Medical condition cannot exceed 200 characters.");

            RuleFor(x => x.Story)
                .NotEmpty().WithMessage("Campaign story is required.")
                .MinimumLength(100).WithMessage("Please tell the full story — at least 100 characters.")
                .MaximumLength(5000).WithMessage("Story cannot exceed 5000 characters.");

            RuleFor(x => x.GoalAmount)
                .GreaterThan(1000).WithMessage("Fundraising goal must be at least ₦1,000.")
                .LessThanOrEqualTo(100_000_000).WithMessage("Fundraising goal cannot exceed ₦100,000,000.");

            RuleFor(x => x.SurgeryDate)
                .GreaterThan(DateTime.UtcNow).WithMessage("Surgery date must be a future date.")
                .When(x => x.SurgeryDate.HasValue);

            RuleFor(x => x.BankName)
                .NotEmpty().WithMessage("Bank name is required.");

            RuleFor(x => x.AccountNumber)
                .NotEmpty().WithMessage("Account number is required.")
                .Matches(@"^\d{10}$").WithMessage("Account number must be exactly 10 digits.");

            RuleFor(x => x.AccountName)
                .NotEmpty().WithMessage("Account name is required.")
                .MaximumLength(100).WithMessage("Account name cannot exceed 100 characters.");
        }
    }
}