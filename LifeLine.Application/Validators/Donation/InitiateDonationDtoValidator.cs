using FluentValidation;
using LifeLine.Application.DTO.Donation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Validators.Donation
{
    public class InitiateDonationDtoValidator : AbstractValidator<InitiateDonationDto>
    {
        public InitiateDonationDtoValidator()
        {
            RuleFor(x => x.CampaignId)
                .NotEmpty().WithMessage("Campaign ID is required.");

            RuleFor(x => x.Amount)
                .GreaterThanOrEqualTo(100).WithMessage("Minimum donation is ₦100.")
                .LessThanOrEqualTo(10_000_000).WithMessage("Maximum single donation is ₦10,000,000.");

            RuleFor(x => x.Message)
                .MaximumLength(300).WithMessage("Message cannot exceed 300 characters.")
                .When(x => x.Message is not null);

            // Guest donation rules — only required when DonorName is provided (meaning they're a guest)
            RuleFor(x => x.DonorName)
                .NotEmpty().WithMessage("Your name is required for guest donations.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.")
                .When(x => x.DonorEmail is not null);

            RuleFor(x => x.DonorEmail)
                .NotEmpty().WithMessage("Your email is required for guest donations.")
                .EmailAddress().WithMessage("Please provide a valid email address.")
                .When(x => x.DonorName is not null);
        }
    }

}