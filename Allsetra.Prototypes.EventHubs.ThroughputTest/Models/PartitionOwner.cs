using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	[DebuggerDisplay( "{" + nameof(Name) + "}" )]
	public class PartitionOwner : INotifyPropertyChanged
	{
		private static readonly List<string> Owners = new List<string>();

		private string _name;
		private DateTime? _hasPartition00Since;
		private DateTime? _hasPartition01Since;
		private DateTime? _hasPartition02Since;
		private DateTime? _hasPartition03Since;
		private DateTime? _hasPartition04Since;
		private DateTime? _hasPartition05Since;
		private DateTime? _hasPartition06Since;
		private DateTime? _hasPartition07Since;
		private DateTime? _hasPartition08Since;
		private DateTime? _hasPartition09Since;
		private DateTime? _hasPartition10Since;
		private DateTime? _hasPartition11Since;
		private DateTime? _hasPartition12Since;
		private DateTime? _hasPartition13Since;
		private DateTime? _hasPartition14Since;
		private DateTime? _hasPartition15Since;
		private DateTime? _hasPartition16Since;
		private DateTime? _hasPartition17Since;
		private DateTime? _hasPartition18Since;
		private DateTime? _hasPartition19Since;
		private DateTime? _hasPartition20Since;
		private DateTime? _hasPartition21Since;
		private DateTime? _hasPartition22Since;
		private DateTime? _hasPartition23Since;
		private DateTime? _hasPartition24Since;
		private DateTime? _hasPartition25Since;
		private DateTime? _hasPartition26Since;
		private DateTime? _hasPartition27Since;
		private DateTime? _hasPartition28Since;
		private DateTime? _hasPartition29Since;
		private DateTime? _hasPartition30Since;
		private DateTime? _hasPartition31Since;

		public string Name
		{
			get { return _name; }
			set
			{
				if ( string.IsNullOrEmpty( value ) && !string.IsNullOrEmpty( Name ) )
				{
					Owners.Remove( Name );
				}

				_name = value;

				if ( !Owners.Contains( Name ) )
				{
					Owners.Add( Name );
				}

				NotifyPropertyChanged();
				NotifyPropertyChanged(nameof(OwnerBrush));
			}
		}

		public Brush OwnerBrush
		{
			get { return MainWindow.OwnerBrushes[( Owners.IndexOf( Name ) + MainWindow.OwnerBrushes.Count ) % MainWindow.OwnerBrushes.Count]; }
		}

		public DateTime? HasPartition00Since
		{
			get { return _hasPartition00Since; }
			set
			{
				_hasPartition00Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition01Since
		{
			get { return _hasPartition01Since; }
			set
			{
				_hasPartition01Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition02Since
		{
			get { return _hasPartition02Since; }
			set
			{
				_hasPartition02Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition03Since
		{
			get { return _hasPartition03Since; }
			set
			{
				_hasPartition03Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition04Since
		{
			get { return _hasPartition04Since; }
			set
			{
				_hasPartition04Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition05Since
		{
			get { return _hasPartition05Since; }
			set
			{
				_hasPartition05Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition06Since
		{
			get { return _hasPartition06Since; }
			set
			{
				_hasPartition06Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition07Since
		{
			get { return _hasPartition07Since; }
			set
			{
				_hasPartition07Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition08Since
		{
			get { return _hasPartition08Since; }
			set
			{
				_hasPartition08Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition09Since
		{
			get { return _hasPartition09Since; }
			set
			{
				_hasPartition09Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition10Since
		{
			get { return _hasPartition10Since; }
			set
			{
				_hasPartition10Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition11Since
		{
			get { return _hasPartition11Since; }
			set
			{
				_hasPartition11Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition12Since
		{
			get { return _hasPartition12Since; }
			set
			{
				_hasPartition12Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition13Since
		{
			get { return _hasPartition13Since; }
			set
			{
				_hasPartition13Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition14Since
		{
			get { return _hasPartition14Since; }
			set
			{
				_hasPartition14Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition15Since
		{
			get { return _hasPartition15Since; }
			set
			{
				_hasPartition15Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition16Since
		{
			get { return _hasPartition16Since; }
			set
			{
				_hasPartition16Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition17Since
		{
			get { return _hasPartition17Since; }
			set
			{
				_hasPartition17Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition18Since
		{
			get { return _hasPartition18Since; }
			set
			{
				_hasPartition18Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition19Since
		{
			get { return _hasPartition19Since; }
			set
			{
				_hasPartition19Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition20Since
		{
			get { return _hasPartition20Since; }
			set
			{
				_hasPartition20Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition21Since
		{
			get { return _hasPartition21Since; }
			set
			{
				_hasPartition21Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition22Since
		{
			get { return _hasPartition22Since; }
			set
			{
				_hasPartition22Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition23Since
		{
			get { return _hasPartition23Since; }
			set
			{
				_hasPartition23Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition24Since
		{
			get { return _hasPartition24Since; }
			set
			{
				_hasPartition24Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition25Since
		{
			get { return _hasPartition25Since; }
			set
			{
				_hasPartition25Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition26Since
		{
			get { return _hasPartition26Since; }
			set
			{
				_hasPartition26Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition27Since
		{
			get { return _hasPartition27Since; }
			set
			{
				_hasPartition27Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition28Since
		{
			get { return _hasPartition28Since; }
			set
			{
				_hasPartition28Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition29Since
		{
			get { return _hasPartition29Since; }
			set
			{
				_hasPartition29Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition30Since
		{
			get { return _hasPartition30Since; }
			set
			{
				_hasPartition30Since = value;
				NotifyPropertyChanged();
			}
		}
		public DateTime? HasPartition31Since
		{
			get { return _hasPartition31Since; }
			set
			{
				_hasPartition31Since = value;
				NotifyPropertyChanged();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void NotifyPropertyChanged( [CallerMemberName] string propertyName = null )
		{
			PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
		}

		public void NotifyPartitionAge()
		{
			NotifyPropertyChanged(nameof(HasPartition00Since));
			NotifyPropertyChanged(nameof(HasPartition01Since));
			NotifyPropertyChanged(nameof(HasPartition02Since));
			NotifyPropertyChanged(nameof(HasPartition03Since));
			NotifyPropertyChanged(nameof(HasPartition04Since));
			NotifyPropertyChanged(nameof(HasPartition05Since));
			NotifyPropertyChanged(nameof(HasPartition06Since));
			NotifyPropertyChanged(nameof(HasPartition07Since));
			NotifyPropertyChanged(nameof(HasPartition08Since));
			NotifyPropertyChanged(nameof(HasPartition09Since));
			NotifyPropertyChanged(nameof(HasPartition10Since));
			NotifyPropertyChanged(nameof(HasPartition11Since));
			NotifyPropertyChanged(nameof(HasPartition12Since));
			NotifyPropertyChanged(nameof(HasPartition13Since));
			NotifyPropertyChanged(nameof(HasPartition14Since));
			NotifyPropertyChanged(nameof(HasPartition15Since));
			NotifyPropertyChanged(nameof(HasPartition16Since));
			NotifyPropertyChanged(nameof(HasPartition17Since));
			NotifyPropertyChanged(nameof(HasPartition18Since));
			NotifyPropertyChanged(nameof(HasPartition19Since));
			NotifyPropertyChanged(nameof(HasPartition20Since));
			NotifyPropertyChanged(nameof(HasPartition21Since));
			NotifyPropertyChanged(nameof(HasPartition22Since));
			NotifyPropertyChanged(nameof(HasPartition23Since));
			NotifyPropertyChanged(nameof(HasPartition24Since));
			NotifyPropertyChanged(nameof(HasPartition25Since));
			NotifyPropertyChanged(nameof(HasPartition26Since));
			NotifyPropertyChanged(nameof(HasPartition27Since));
			NotifyPropertyChanged(nameof(HasPartition28Since));
			NotifyPropertyChanged(nameof(HasPartition29Since));
			NotifyPropertyChanged(nameof(HasPartition30Since));
			NotifyPropertyChanged(nameof(HasPartition31Since));
		}
	}
}