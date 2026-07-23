using System.Collections.Generic;
using UnityEngine;

namespace PositionOrder {

    public enum Axis2D { XY, XZ, ZY }

    public enum LineAnchor { Start, Center, End, Custom }
    public enum TableAnchor { TopLeft, TopMiddle, TopRight, MiddleLeft, Center, MiddleRight, BottomLeft, BottomMiddle, BottomRight, Custom }
    public enum CubeAnchor { Up, Forward, Left, Center, Right, Back, Down, Custom }

    public class PositionOrderer {

        private enum WarningRequest { TransformOnly, Column, ColumnAndRow }

        public float Distance_X { get; set; }
        public float Distance_Y { get; set; }
        public float Distance_Z { get; set; }

        public List<Transform> Transforms { get; set; } 

        public const int MIN_COUNT = 2; 

        
        public PositionOrderer () {
            Transforms = new List<Transform> ();
        }

        #region Apply

        
        public void ApplyLineOrder (LineAnchor anchor, int customIndex = 0) {

           
            if (!IsCountSafe (WarningRequest.TransformOnly)) {
                return;
            }

            int idx = 0;
            if (anchor == LineAnchor.Custom) {
                if (!TrySetCustomIndex (customIndex, out idx)) {
                    return;
                }

            } else { 
                idx = GetStandardIndexFromLineAnchor (anchor);
            }

            int count = Transforms.Count;

            Vector3 startPos = Transforms[idx].position; 
            Vector3 dist = Vector3.zero; 

           
            for (int i = 0; i < count; i++) {
                dist.Set ((i - idx) * Distance_X, (i - idx) * Distance_Y, (i - idx) * Distance_Z);
                Transforms[i].position = startPos + dist;
            }
        }

       
        public void ApplyTableOrder (TableAnchor anchor, Axis2D axis, int col, int customIndex = 0) {

            
            if (!IsCountSafe (WarningRequest.Column, col)) {
                return;
            }

            int idx = 0;
            if (anchor == TableAnchor.Custom) {
                if (!TrySetCustomIndex (customIndex, out idx)) {
                    return;
                }

            } else { 
                idx = GetStandardIndexFromTableAnchor (anchor, col);
            }

           
            int start_col = idx % col;
            int start_row = idx / col;
            int curr_col, curr_row;

            Vector3 startPos = Transforms[idx].position;
            Vector3 dist = Vector3.zero;

            
            for (int i = 0; i < Transforms.Count; i++) {
                curr_col = i % col;
                curr_row = i / col;
                SetDistanceByAxis2D (ref dist, axis, curr_col - start_col, start_row - curr_row);
                Transforms[i].position = startPos + dist;
            }
        }

       
        private void SetDistanceByAxis2D (ref Vector3 vector, Axis2D axis, float col, float row) {
            switch (axis) {
                case Axis2D.XY:
                    vector.Set (col * Distance_X, row * Distance_Y, 0);
                    break;
                case Axis2D.XZ:
                    vector.Set (col * Distance_X, 0, row * Distance_Z);
                    break;
                case Axis2D.ZY:
                    vector.Set (0, row * Distance_Y, col * Distance_Z);
                    break;
            }
        }

        
        public void ApplyCubeOrder (CubeAnchor anchor, int col, int row, int customIndex = 0) {

            if (!IsCountSafe (WarningRequest.ColumnAndRow, col, row)) {
                return;
            }

            int idx = 0;
            if (anchor == CubeAnchor.Custom) { 
                if (!TrySetCustomIndex (customIndex, out idx)) {
                    return;
                }

            } else {
                idx = GetStandardIndexFromCubeAnchor (anchor, col, row);
            }

           
            int floor_count = col * row;
            int start_height = idx / floor_count;
            int start_col = idx % col;
            int start_row = (idx % floor_count) / col;
            int curr_col, curr_row, curr_height;

            Vector3 startPos = Transforms[idx].position; 
            Vector3 dist = Vector3.zero; 

            
            for (int i = 0; i < Transforms.Count; i++) {
                curr_height = i / floor_count;
                curr_col = i % col;
                curr_row = (i % floor_count) / col;
                dist.Set ((curr_col - start_col) * Distance_X, (start_height - curr_height) * Distance_Y, (start_row - curr_row) * Distance_Z);
                Transforms[i].position = startPos + dist;
            }
        }

        #endregion

        #region Common

        private bool IsCountSafe (WarningRequest warning, int col = 0, int row = 0) {

            
            if (Transforms.Count < MIN_COUNT) {
                Debug.LogWarning ("Transform count in list is too small.");
                return false;
            }

            if (warning == WarningRequest.Column || warning == WarningRequest.ColumnAndRow) {

                
                if (col < MIN_COUNT) {
                    Debug.LogWarning ("Column count is too small.");
                    return false;
                }

                if (warning == WarningRequest.ColumnAndRow) {
                    
                    if (row < MIN_COUNT) {
                        Debug.LogWarning ("Row count is too small.");
                        return false;
                    }
                }
            }

            return true; 
        }

        private bool TrySetCustomIndex (int customIndex, out int result) {
            if (customIndex >= 0 && customIndex < Transforms.Count) { 
                result = customIndex;
                return true;

            } else {
                Debug.LogError ("Custom index out of range: " + customIndex);
                result = -1;
                return false;
            }
        }

        #endregion

        #region Get Standard Index

       
        private int GetStandardIndexFromLineAnchor (LineAnchor anchor) {
            int count = Transforms.Count;

            switch (anchor) {
                case LineAnchor.Start:
                    return 0;
                case LineAnchor.Center:
                    return count / 2;
                case LineAnchor.End:
                    return count - 1;
                default:
                    return -1;
            }
        }

        
        private int GetStandardIndexFromTableAnchor (TableAnchor anchor, int col) {
            int count = Transforms.Count;
            int row = Mathf.CeilToInt ((float)count / col);

            switch (anchor) {
                case TableAnchor.TopLeft:
                    return 0;
                case TableAnchor.TopMiddle:
                    return col / 2;
                case TableAnchor.TopRight:
                    return col - 1;
                case TableAnchor.MiddleLeft:
                    return col * (row / 2);
                case TableAnchor.Center:
                    return count / 2;
                case TableAnchor.MiddleRight:
                    return (col * (row / 2)) + (col - 1);
                case TableAnchor.BottomLeft:
                    return row * (col - 1);
                case TableAnchor.BottomMiddle:
                    return (row * (col - 1)) + (row / 2);
                case TableAnchor.BottomRight:
                    return count - 1;
                default:
                    return -1;
            }
        }

        
        private int GetStandardIndexFromCubeAnchor (CubeAnchor anchor, int col, int row) {
            int count = Transforms.Count;
            int floor_count = col * row;
            int mid_height_idx = floor_count * (Mathf.CeilToInt ((float)count / floor_count) / 2);
            int mid_row_idx = floor_count / col;

            switch (anchor) {
                case CubeAnchor.Up:
                    return floor_count / 2;
                case CubeAnchor.Forward:
                    return mid_height_idx + (col / 2);
                case CubeAnchor.Left:
                    return mid_height_idx + mid_row_idx;
                case CubeAnchor.Center:
                    return count / 2;
                case CubeAnchor.Right:
                    return mid_height_idx + mid_row_idx + (col - 1);
                case CubeAnchor.Back:
                    return mid_height_idx + (floor_count - ((col / 2) + 1));
                case CubeAnchor.Down:
                    return (count - 1) - (floor_count / 2);
                default:
                    return -1;
            }
        }

        #endregion
    }
}
