using System;
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

        /// <summary>Chamado quando o usuário marca/desmarca a função (para aplicar as áreas que ela libera).</summary>
        public Action<ProfessionalRoleOption>? CheckedChanged { get; set; }

        partial void OnIsCheckedChanged(bool value) => CheckedChanged?.Invoke(this);
    }

    // Marcar uma função marca na grade as áreas que ela libera (UserAuthorization.PoliciesGrantedByRole);
    // desmarcar tira só o que nenhuma outra função marcada também libera.
    public static class ProfessionalRolePresets
    {
        // Funções que existem no servidor mas não aparecem na tela (quem já tem continua tendo: a tela só remove o
        // que mostra, ver ProfessionalPermissionsDiff).
        private static readonly HashSet<string> HiddenRoles = new(StringComparer.Ordinal) { UserAuthorization.Roles.User };

        public static bool IsVisibleRole(string role) => !HiddenRoles.Contains(role);

        public static void Apply(IEnumerable<ProfessionalPolicyDomainRow> rows, IEnumerable<ProfessionalRoleOption> roles, ProfessionalRoleOption changed)
        {
            var granted = UserAuthorization.GetPoliciesGrantedByRoles(new[] { changed.Name });
            if (granted.Count == 0)
                return;

            var rowList = rows.ToList();
            if (changed.IsChecked)
            {
                foreach (var policy in granted)
                    foreach (var row in rowList)
                        row.SetPolicy(policy, true);
                return;
            }

            var stillGranted = UserAuthorization.GetPoliciesGrantedByRoles(
                roles.Where(r => r != changed && r.IsChecked).Select(r => r.Name));
            foreach (var policy in granted.Where(p => !stillGranted.Contains(p)))
                foreach (var row in rowList)
                    row.SetPolicy(policy, false);
            // Desmarcar um "Tudo" (ex.: Administrador) limpa a linha inteira: remarca o que as funções ainda
            // marcadas liberam (ex.: Separador continua com Ver/Criar/Editar em Coleções).
            foreach (var policy in stillGranted)
                foreach (var row in rowList)
                    row.SetPolicy(policy, true);
        }
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

        /// <summary>Marca/desmarca a caixa da policy "Domínio.AÇÃO" se for desta linha (e a ação existir nela).</summary>
        public void SetPolicy(string policy, bool value)
        {
            var dot = policy.IndexOf('.');
            if (dot < 0 || policy[..dot] != Domain)
                return;
            switch (policy[(dot + 1)..])
            {
                case View: if (HasView) CanView = value; break;
                case Create: if (HasCreate) CanCreate = value; break;
                case Edit: if (HasEdit) CanEdit = value; break;
                case Delete: if (HasDelete) CanDelete = value; break;
                case All: CanAll = value; break;
            }
        }

        // Marca as caixas conforme as policies que o professional já tem ("Domínio.ALL" marca todas).
        public void LoadFrom(ICollection<string> policies)
        {
            if (policies.Contains($"{Domain}.{All}"))
            {
                CanAll = true;
                return;
            }
            CanView = HasView && policies.Contains($"{Domain}.{View}");
            CanCreate = HasCreate && policies.Contains($"{Domain}.{Create}");
            CanEdit = HasEdit && policies.Contains($"{Domain}.{Edit}");
            CanDelete = HasDelete && policies.Contains($"{Domain}.{Delete}");
        }

        // Todas as policies que a linha consegue representar (as que a tela pode tirar).
        public IEnumerable<string> RepresentablePolicies()
        {
            if (HasView) yield return $"{Domain}.{View}";
            if (HasCreate) yield return $"{Domain}.{Create}";
            if (HasEdit) yield return $"{Domain}.{Edit}";
            if (HasDelete) yield return $"{Domain}.{Delete}";
            if (HasAll) yield return $"{Domain}.{All}";
        }
    }

    // Diferença entre as permissões atuais e as escolhidas na tela, no formato de UpdateUserPoliciesAndRoles_v2
    // (adicionar/remover). Só remove o que a tela mostra: role/policy que o professional tenha e não esteja no
    // catálogo (nome antigo, criada à mão) fica como está.
    public sealed class ProfessionalPermissionsDiff
    {
        public List<string> RolesToAdd { get; init; } = new();
        public List<string> RolesToRemove { get; init; } = new();
        public List<string> PoliciesToAdd { get; init; } = new();
        public List<string> PoliciesToRemove { get; init; } = new();

        public bool IsEmpty => RolesToAdd.Count == 0 && RolesToRemove.Count == 0 && PoliciesToAdd.Count == 0 && PoliciesToRemove.Count == 0;

        public static ProfessionalPermissionsDiff Compute(
            UserAuthorizationConfig? current,
            UserAuthorizationConfig chosen,
            IEnumerable<string> catalogRoles,
            IEnumerable<string> catalogPolicies)
        {
            var oldRoles = current?.Roles ?? new HashSet<string>();
            var oldPolicies = current?.Policies ?? new HashSet<string>();
            var shownRoles = catalogRoles.ToHashSet();
            var shownPolicies = catalogPolicies.ToHashSet();

            return new ProfessionalPermissionsDiff
            {
                RolesToAdd = chosen.Roles.Where(r => !oldRoles.Contains(r)).OrderBy(x => x).ToList(),
                RolesToRemove = oldRoles.Where(r => shownRoles.Contains(r) && !chosen.Roles.Contains(r)).OrderBy(x => x).ToList(),
                PoliciesToAdd = chosen.Policies.Where(p => !oldPolicies.Contains(p)).OrderBy(x => x).ToList(),
                PoliciesToRemove = oldPolicies.Where(p => shownPolicies.Contains(p) && !chosen.Policies.Contains(p)).OrderBy(x => x).ToList(),
            };
        }
    }
}
