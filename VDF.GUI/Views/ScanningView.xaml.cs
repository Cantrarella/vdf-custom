// /*
//     Copyright (C) 2026 0x90d
//     This file is part of VideoDuplicateFinder
//     VideoDuplicateFinder is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//     VideoDuplicateFinder is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//     You should have received a copy of the GNU Affero General Public License
//     along with VideoDuplicateFinder.  If not, see <http://www.gnu.org/licenses/>.
// */
//

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VDF.GUI.Views {
	public partial class ScanningView : UserControl {
		public ScanningView() {
			AvaloniaXamlLoader.Load(this);
		}
	}

	/// <summary>The mockup's .ring-fg: the arc of the 168px ring that is filled in.
	/// A ring is not something Avalonia's own progress bar draws, and the two obvious
	/// ways to fake it both fail: an Ellipse with a dash array needs a string the
	/// binding layer will not parse into the dash collection, and a full ellipse has
	/// no end to put the round cap on. So the arc is a Path, and this is the geometry.
	/// The circle is r=70 about (84,84) — the mockup's own numbers — and starts at the
	/// top so a part-filled ring opens clockwise from 12 o'clock the way the mockup
	/// draws it. A whole turn cannot be one arc (its ends coincide and the geometry
	/// collapses), so it stops a hair short and 100% reads as a closed ring.</summary>
	public sealed class ScanRingArcConverter : IMultiValueConverter {
		const double Radius = 70d;
		static readonly Point Center = new(84d, 84d);

		public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) {
			double value = values.Count > 0 && values[0] is double d ? d
						 : values.Count > 0 && values[0] is int i ? i
						 : 0d;
			double maximum = values.Count > 1 && values[1] is double md ? md
						   : values.Count > 1 && values[1] is int mi ? mi
						   : 0d;
			double fraction = maximum > 0d ? Math.Clamp(value / maximum, 0d, 1d) : 0d;
			// One degree short of a full turn: a 360° arc has identical start and end
			// points and draws nothing.
			double sweep = Math.Min(359.99d, 360d * fraction);
			if (sweep <= 0d)
				return null;
			Point start = new(Center.X, Center.Y - Radius);
			Point end = PointOnCircle(sweep);
			return new PathGeometry {
				Figures = new PathFigures {
					new PathFigure {
						StartPoint = start,
						Segments = new PathSegments {
							new ArcSegment {
								Point = end,
								Size = new Size(Radius, Radius),
								RotationAngle = 0d,
								IsLargeArc = sweep > 180d,
								SweepDirection = SweepDirection.Clockwise,
							},
						},
					},
				},
			};
		}

		static Point PointOnCircle(double degrees) {
			double radians = (degrees - 90d) * Math.PI / 180d;
			return new Point(Center.X + Radius * Math.Cos(radians), Center.Y + Radius * Math.Sin(radians));
		}
	}
}
