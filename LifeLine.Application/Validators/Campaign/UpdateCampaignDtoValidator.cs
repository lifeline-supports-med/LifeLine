using FluentValidation;
using LifeLine.Application.DTO.Campaign;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Validators.Campaign
{
    public class UpdateCampaignDtoValidator : AbstractValidator<UpdateCampaignDto>
    {
        public UpdateCampaignDtoValidator()
        {
            RuleFor(x => x.Title)
                .MinimumLength(10).WithMessage("Title must be at least 10 characters.")
                .MaximumLength(150).WithMessage("Title cannot exceed 150 characters.")
                .When(x => x.Title is not null);

            RuleFor(x => x.Story)
                .MinimumLength(100).WithMessage("Story must be at least 100 characters.")
                .MaximumLength(5000).WithMessage("Story cannot exceed 5000 characters.")
                .When(x => x.Story is not null);

            RuleFor(x => x.GoalAmount)
                .GreaterThan(1000).WithMessage("Fundraising goal must be at least ₦1,000.")
                .When(x => x.GoalAmount.HasValue);

            RuleFor(x => x.SurgeryDate)
                .GreaterThan(DateTime.UtcNow).WithMessage("Surgery date must be a future date.")
                .When(x => x.SurgeryDate.HasValue);

            RuleFor(x => x.AccountNumber)
                .Matches(@"^\d{10}$").WithMessage("Account number must be exactly 10 digits.")
                .When(x => x.AccountNumber is not null);
        }

    }

}