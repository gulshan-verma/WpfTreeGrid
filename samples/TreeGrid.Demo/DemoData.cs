using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace TreeGrid.Demo
{
    /// <summary>
    /// Exercises three validation sources at once: DataAnnotations on the properties,
    /// IDataErrorInfo for cross-field rules, and IEditableObject for row rollback.
    /// </summary>
    public class Employee : INotifyPropertyChanged, IDataErrorInfo, IEditableObject
    {
        private string _firstName;
        private string _lastName;
        private string _title;
        private double _salary;
        private bool _available;
        private int _completion;

        private Employee _snapshot;

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(30, ErrorMessage = "First name must be 30 characters or fewer.")]
        public string FirstName
        {
            get => _firstName;
            set => Set(ref _firstName, value);
        }

        public string LastName
        {
            get => _lastName;
            set => Set(ref _lastName, value);
        }

        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        [Range(0, 500000, ErrorMessage = "Salary must be between 0 and 500,000.")]
        public double Salary
        {
            get => _salary;
            set => Set(ref _salary, value);
        }

        public bool Available
        {
            get => _available;
            set => Set(ref _available, value);
        }

        public int Completion
        {
            get => _completion;
            set => Set(ref _completion, value);
        }

        public string ProfileUrl { get; set; }

        private bool _isSelected;
        private bool _isChecked;

        /// <summary>Mapped to row selection via SelectedMemberPath.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        /// <summary>Mapped to the hierarchy checkbox via CheckedMemberPath.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }

        public ObservableCollection<Employee> Children { get; set; } = new ObservableCollection<Employee>();

        /// <summary>Cross-field rule: annotations cannot see two properties at once.</summary>
        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(Salary) && Title == "Director" && Salary < 100000)
                    return "A Director must earn at least 100,000.";

                return null;
            }
        }

        public string Error => null;

        public void BeginEdit()
        {
            _snapshot ??= new Employee
            {
                _firstName = _firstName,
                _lastName = _lastName,
                _title = _title,
                _salary = _salary,
                _available = _available,
                _completion = _completion
            };
        }

        public void CancelEdit()
        {
            if (_snapshot == null)
                return;

            FirstName = _snapshot._firstName;
            LastName = _snapshot._lastName;
            Title = _snapshot._title;
            Salary = _snapshot._salary;
            Available = _snapshot._available;
            Completion = _snapshot._completion;

            _snapshot = null;
        }

        public void EndEdit() => _snapshot = null;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString() => $"{FirstName} {LastName}";
    }

    /// <summary>
    /// Flat rows linked by Id / ParentId, for the self-relational demo.
    /// Deliberately not called "Task": that would collide with
    /// System.Threading.Tasks.Task in any file that also does async work.
    /// </summary>
    public class ProjectTask
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public int PercentComplete { get; set; }
        public DateTime Start { get; set; }
    }

    /// <summary>A node in the lazily-generated tree used by the load-on-demand demo.</summary>
    public class FolderItem
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public long SizeBytes { get; set; }
        public int Depth { get; set; }
        public bool IsFolder { get; set; }
    }

    public static class DemoData
    {
        private static readonly string[] FirstNames =
        {
            "Andrew", "Theodore", "Ronald", "Steven", "Nancy", "Janet",
            "Margaret", "Laura", "Anne", "Michael", "Priya", "Rahul"
        };

        private static readonly string[] LastNames =
        {
            "Fuller", "Hoover", "Fillmore", "Buchanan", "Davolio", "Leverling",
            "Peacock", "Callahan", "Dodsworth", "Suyama", "Sharma", "Iyer"
        };

        private static readonly string[] Titles =
        {
            "Director", "Manager", "Team Lead", "Senior Engineer", "Engineer", "Analyst"
        };

        public static ObservableCollection<Employee> CreateHierarchy(int rootCount = 8, int depth = 4, int childrenPerNode = 4)
        {
            var random = new Random(42);
            var roots = new ObservableCollection<Employee>();

            for (var i = 0; i < rootCount; i++)
                roots.Add(BuildEmployee(random, 0, depth, childrenPerNode));

            return roots;
        }

        private static Employee BuildEmployee(Random random, int level, int maxDepth, int childrenPerNode)
        {
            var employee = new Employee
            {
                FirstName = FirstNames[random.Next(FirstNames.Length)],
                LastName = LastNames[random.Next(LastNames.Length)],
                Title = Titles[Math.Min(level, Titles.Length - 1)],
                Salary = Math.Round((double)(200000 - level * 30000 + random.Next(-15000, 15000)), 0),
                Available = random.Next(0, 2) == 1,
                Completion = random.Next(0, 101),
                ProfileUrl = "https://example.com/staff"
            };

            if (level < maxDepth)
            {
                for (var i = 0; i < childrenPerNode; i++)
                    employee.Children.Add(BuildEmployee(random, level + 1, maxDepth, childrenPerNode));
            }

            return employee;
        }

        public static ObservableCollection<ProjectTask> CreateSelfRelational(int count = 5000)
        {
            var random = new Random(7);
            var list = new ObservableCollection<ProjectTask>();
            var idsByLevel = new List<List<int>> { new List<int>() };

            for (var id = 1; id <= count; id++)
            {
                var level = 0;

                if (id > 1)
                {
                    // Bias towards shallow trees so the demo stays readable.
                    level = Math.Min(idsByLevel.Count, random.Next(0, 4));
                }

                int? parentId = null;

                if (level > 0 && idsByLevel.Count >= level && idsByLevel[level - 1].Count > 0)
                {
                    var candidates = idsByLevel[level - 1];
                    parentId = candidates[random.Next(candidates.Count)];
                }
                else
                {
                    level = 0;
                }

                list.Add(new ProjectTask
                {
                    Id = id,
                    ParentId = parentId,
                    Name = $"Task {id}",
                    Owner = $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}",
                    PercentComplete = random.Next(0, 101),
                    Start = new DateTime(2026, 1, 1).AddDays(random.Next(0, 240))
                });

                while (idsByLevel.Count <= level)
                    idsByLevel.Add(new List<int>());

                idsByLevel[level].Add(id);
            }

            return list;
        }

        /// <summary>
        /// Fabricates children on demand. Stands in for a database or REST call: the
        /// grid never knows how large the tree is.
        /// </summary>
        public static List<FolderItem> CreateFolderChildren(FolderItem parent, int count = 40)
        {
            var depth = (parent?.Depth ?? -1) + 1;
            var random = new Random((parent?.Name?.GetHashCode() ?? 0) ^ depth);
            var items = new List<FolderItem>();

            for (var i = 0; i < count; i++)
            {
                var isFolder = depth < 6 && i % 4 == 0;

                items.Add(new FolderItem
                {
                    Name = isFolder ? $"Folder {depth}.{i}" : $"file-{depth}-{i}.dat",
                    Kind = isFolder ? "Folder" : "File",
                    SizeBytes = isFolder ? 0 : random.Next(1024, 50_000_000),
                    Depth = depth,
                    IsFolder = isFolder
                });
            }

            return items;
        }
    }
}
