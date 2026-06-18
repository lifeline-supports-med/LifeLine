using FluentValidation;
using LifeLine.Application.DTO.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Validators.Authentications
{
    public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordDtoValidator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Current password is required.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .Matches("[A-Z]").WithMessage("Must contain at least one uppercase letter.")
                .Matches("[a-z]").WithMessage("Must contain at least one lowercase letter.")
                .Matches("[0-9]").WithMessage("Must contain at least one number.")
                .NotEqual(x => x.CurrentPassword)
                .WithMessage("New password must differ from current password.");
        }
    }
}
