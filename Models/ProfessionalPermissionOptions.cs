using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient.Models
{
    // Permissões de um professional na tela de cadastro, montadas a partir do catálogo do servidor
    // (GetUserAuthorizationOptions_v2). Policy = "Domínio.AÇÃO"; AÇÃO = GET/POST/PUT/DELETE ou ALL (todas).

    public partial class ProfessionalRoleOption : ObservableObject
    {
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        [ObservableProperty] private bool isChecked;
    }

    public partial class ProfessionalPolicyDomainRow : ObservableObject
    {
        public const string View = "GET", Create = "POST", Edit = "PUT", Delete = "DELETE", All = "ALL";

        public string Domain { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        // Ações que o servidor oferece neste domínio (as caixas das ausentes ficam escondidas).
        public bool HasView { get; init; }
        public bool HasCreate { get; init; }
        public bool HasEdit { get; init; }
        public bool HasDelete { get; init; }
        public bool HasAll { get; init; }

        [ObservableProperty] private bool canView;
        [ObservableProperty] private bool canCreate;
        [ObservableProperty] private bool canEdit;
        [ObservableProperty] private bool canDelete;

        private bool _syncingAll;

        // "Tudo" marcado = todas as ações existentes marcadas; marcar/desmarcar "Tudo" marca/desmarca todas.
        public bool CanAll
        {
            get => (!HasView || CanView) && (!HasCreate || CanCreate) && (!HasEdit || CanEdit) && (!HasDelete || CanDelete)
                   && (HasView || HasCreate || HasEdit || HasDelete);
            set
            {
                _syncingAll = true;
                try
                {
                    if (HasView) CanView = value;
                    if (HasCreate) CanCreate = value;
                    if (HasEdit) CanEdit = value;
                    if (HasDelete) CanDelete = value;
                }
                finally { _syncingAll = false; }
                OnPropertyChanged(nameof(CanAll));
            }
        }

        partial void OnCanViewChanged(bool value) => NotifyAll();
        partial void OnCanCreateChanged(bool value) => NotifyAll();
        partial void OnCanEditChanged(bool value) => NotifyAll();
        partial void OnCanDeleteChanged(bool value) => NotifyAll();
        private void NotifyAll()
        {
            if (!_syncingAll)
                OnPropertyChanged(nameof(CanAll));
        }

        public static ProfessionalPolicyDomainRow From(PolicyDomain d, string displayName)
        {
            var actions = (d.Actions ?? new List<string>()).Select(a => a.Substring(a.IndexOf('.') + 1)).ToHashSet();
            return new ProfessionalPolicyDomainRow
            {
                Domain = d.Domain,
                DisplayName = displayName,
                HasView = actions.Contains(View),
                HasCreate = actions.Contains(Create),
                HasEdit = actions.Contains(Edit),
                HasDelete = actions.Contains(Delete),
                HasAll = actions.Contains(All)
            };
        }

        // Todas marcadas e o domínio tem ALL: grava só "Domínio.ALL" (cobre ações que surgirem depois no mesmo
        // domínio, ver AuthorizationMiddleware.HasPolicy). Senão, uma policy por ação marcada.
        public IEnumerable<string> ToPolicies()
        {
            if (HasAll && CanAll)
            {
                yield return $"{Domain}.{All}";
                yield break;
            }
            if (HasView && CanView) yield return $"{Domain}.{View}";
            if (HasCreate && CanCreate) yield return $"{Domain}.{Create}";
            if (HasEdit && CanEdit) yield return $"{Domain}.{Edit}";
            if (HasDelete && CanDelete) yield return $"{Domain}.{Delete}";
        }

        public bool AnyChecked => CanView || CanCreate || CanEdit || CanDelete;
    }
}
