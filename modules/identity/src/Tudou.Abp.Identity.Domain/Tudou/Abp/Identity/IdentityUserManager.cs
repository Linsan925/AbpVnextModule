﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Threading;

namespace Tudou.Abp.Identity
{
    public class IdentityUserManager : UserManager<IdentityUser>, IDomainService
    {
        protected IIdentityRoleRepository RoleRepository { get; }
        protected IIdentityUserRepository UserRepository { get; }
        protected IdentityUserOrganizationUnitRepository UserOrganizationUnitRepository { get; }
        protected override CancellationToken CancellationToken => CancellationTokenProvider.Token;

        protected ICancellationTokenProvider CancellationTokenProvider { get; }

        public IdentityUserManager(
            IdentityUserStore store,
            IIdentityRoleRepository roleRepository,
            IIdentityUserRepository userRepository,
            IOptions<IdentityOptions> optionsAccessor,
            IPasswordHasher<IdentityUser> passwordHasher,
            IEnumerable<IUserValidator<IdentityUser>> userValidators,
            IEnumerable<IPasswordValidator<IdentityUser>> passwordValidators,
            IdentityUserOrganizationUnitRepository userOrganizationUnitRepository,
            ILookupNormalizer keyNormalizer,
            IdentityErrorDescriber errors,
            IServiceProvider services,
            ILogger<IdentityUserManager> logger,
            ICancellationTokenProvider cancellationTokenProvider)
            : base(
                  store,
                  optionsAccessor,
                  passwordHasher,
                  userValidators,
                  passwordValidators,
                  keyNormalizer,
                  errors,
                  services,
                  logger)
        {
            RoleRepository = roleRepository;
            UserOrganizationUnitRepository = userOrganizationUnitRepository;
            UserRepository = userRepository;
            CancellationTokenProvider = cancellationTokenProvider;
        }

        public virtual async Task<IdentityUser> GetByIdAsync(Guid id)
        {
            var user = await Store.FindByIdAsync(id.ToString(), CancellationToken);
            if (user == null)
            {
                throw new EntityNotFoundException(typeof(IdentityUser), id);
            }

            return user;
        }

        public virtual async Task<IdentityResult> SetRolesAsync([NotNull] IdentityUser user, [NotNull] IEnumerable<string> roleNames)
        {
            Check.NotNull(user, nameof(user));
            Check.NotNull(roleNames, nameof(roleNames));
            
            var currentRoleNames = await GetRolesAsync(user);

            var result = await RemoveFromRolesAsync(user, currentRoleNames.Except(roleNames).Distinct());
            if (!result.Succeeded)
            {
                return result;
            }

            result = await AddToRolesAsync(user, roleNames.Except(currentRoleNames).Distinct());
            if (!result.Succeeded)
            {
                return result;
            }

            return IdentityResult.Success;
        }
        public virtual async Task RemoveFromOrganizationUnitAsync(Guid userId, Guid ouId)
        {
            await UserOrganizationUnitRepository.DeleteAsync(uou => uou.UserId == userId && uou.OrganizationUnitId == ouId);
        }
        public virtual async Task AddToOrganizationUnitAsync(Guid userId, Guid ouId)
        {
            var currentOu = await UserOrganizationUnitRepository.FindAsync(t => t.UserId == userId && t.OrganizationUnitId == ouId);

            if (currentOu!=null)
            {
                return;
            }
            var user = await UserRepository.GetAsync(userId);
            await UserOrganizationUnitRepository.InsertAsync(new IdentityUserOrganizationUnit(user.TenantId, userId, ouId));
        }
        public virtual async Task<IdentityResult> AddDefaultRolesAsync([NotNull] IdentityUser user)
        {
            await UserRepository.EnsureCollectionLoadedAsync(user, u => u.Roles, CancellationToken);
            
            foreach (var role in await RoleRepository.GetDefaultOnesAsync(cancellationToken: CancellationToken))
            {
                if (!user.IsInRole(role.Id))
                {
                    user.AddRole(role.Id);
                }
            }
            
            return await UpdateUserAsync(user);
        }
    }
}
